using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using FypMatchTracker.Data;
using FypMatchTracker.Models;

namespace FypMatchTracker.Services;

/// <summary>
/// Core similarity evaluation service.
/// Coordinates the Meaning score (70%) and Keyword score (30%) algorithms.
/// Matches the Class Diagram and Flowchart in Figures 1, 3, & 4 of the assignment document.
/// </summary>
public class SimilarityService
{
    private readonly ApplicationDbContext _context;
    private readonly EmbeddingService _embeddingService;
    private readonly KeywordService _keywordService;
    private readonly CrossrefService _crossrefService;

    public SimilarityService(
        ApplicationDbContext context,
        EmbeddingService embeddingService,
        KeywordService keywordService,
        CrossrefService crossrefService)
    {
        _context = context;
        _embeddingService = embeddingService;
        _keywordService = keywordService;
        _crossrefService = crossrefService;
    }

    /// <summary>
    /// Executes the full idea-checking pipeline:
    /// 1. Analyzes text semantics & keywords
    /// 2. Compares with saved department FYPs
    /// 3. Queries Crossref Web API for published literature (and stores for reuse)
    /// 4. Calculates weighted final similarity %
    /// 5. Assigns verdict (UNIQUE, SIMILAR, ALREADY DONE)
    /// 6. Saves check & matches to the database
    /// </summary>
    public async Task<SimilarityCheck> CheckIdeaAsync(
        string title, 
        string description, 
        string? keywords, 
        string? departmentName, 
        int? studentId = null,
        string? studentName = null,
        string? universityName = null,
        string? supervisorName = null)
    {
        // 1. Fetch current settings from database
        var settings = await _context.SimilaritySettings.FirstOrDefaultAsync() ?? new SimilaritySetting();

        string fullSubmissionText = string.IsNullOrWhiteSpace(description) 
            ? title 
            : $"{title}. {description}";

        if (!string.IsNullOrWhiteSpace(keywords))
        {
            fullSubmissionText += $" Tools: {keywords}";
        }

        // Generate query embedding using local ONNX model
        var queryEmbedding = _embeddingService.GetEmbedding(fullSubmissionText);

        var candidateMatches = new List<SimilarityMatch>();

        // 2. Compare against Department Saved FYPs
        var savedProjects = await _context.FypProjects.ToListAsync();

        foreach (var project in savedProjects)
        {
            var (finalScore, semanticScore, keywordScore) = CalculateMultiTierSimilarity(
                title,
                description,
                keywords,
                project.Title,
                project.Description,
                project.Keywords,
                project.EmbeddingJson,
                settings);

            candidateMatches.Add(new SimilarityMatch
            {
                FypProjectId = project.Id,
                FypProject = project,
                SemanticScore = semanticScore,
                KeywordScore = keywordScore,
                FinalScore = finalScore
            });
        }

        // 3. Query External Crossref Web API (if enabled in settings)
        if (settings.CrossrefApiEnabled)
        {
            try
            {
                var externalPapers = await _crossrefService.SearchPapersAsync(title);

                foreach (var paper in externalPapers)
                {
                    // Check if paper already exists in database or change tracker by DOI (prevent duplicates)
                    var existingPaper = await _context.ExternalPapers.FirstOrDefaultAsync(p => p.Doi == paper.Doi)
                                        ?? _context.ExternalPapers.Local.FirstOrDefault(p => p.Doi == paper.Doi);
                    if (existingPaper == null)
                    {
                        // Generate embedding and save to database for reuse (Requirement 4 & Module 5)
                        string paperText = $"{paper.Title}. {paper.Abstract ?? ""}";
                        paper.EmbeddingJson = _embeddingService.GetEmbeddingJson(paperText);
                        _context.ExternalPapers.Add(paper);
                        await _context.SaveChangesAsync();
                        existingPaper = paper;
                    }

                    var (finalScore, semanticScore, keywordScore) = CalculateMultiTierSimilarity(
                        title,
                        description,
                        keywords,
                        existingPaper.Title,
                        existingPaper.Abstract,
                        null,
                        existingPaper.EmbeddingJson,
                        settings);

                    candidateMatches.Add(new SimilarityMatch
                    {
                        ExternalPaperId = existingPaper.Id,
                        ExternalPaper = existingPaper,
                        SemanticScore = semanticScore,
                        KeywordScore = keywordScore,
                        FinalScore = finalScore
                    });
                }
            }
            catch
            {
                // Fallback: If Crossref is down or offline, the check continues seamlessly with local data
            }
        }

        // 4. Rank matches by highest similarity (filter out negligible matches < 10%)
        var topMatches = candidateMatches
            .Where(m => m.FinalScore >= 10.0)
            .OrderByDescending(m => m.FinalScore)
            .Take(5)
            .ToList();

        // Department archive matches determine whether proposal is already done in department
        double topArchiveScore = candidateMatches
            .Where(m => m.FypProjectId != null)
            .Select(m => m.FinalScore)
            .DefaultIfEmpty(0.0)
            .Max();

        double topExternalScore = candidateMatches
            .Where(m => m.ExternalPaperId != null)
            .Select(m => m.FinalScore)
            .DefaultIfEmpty(0.0)
            .Max();

        // If an external paper matches, it marks as ALREADY DONE only if it is a truly high duplicate (> 70%)
        double topScore = Math.Max(topArchiveScore, topExternalScore >= 70.0 ? topExternalScore : topExternalScore * 0.75);
        topScore = Math.Round(topScore, 1);

        string verdict = GetVerdict(topScore, settings);

        // Resolve student details if studentId provided
        if (studentId.HasValue && (string.IsNullOrWhiteSpace(studentName) || string.IsNullOrWhiteSpace(universityName)))
        {
            var user = await _context.Users.FindAsync(studentId.Value);
            if (user != null)
            {
                if (string.IsNullOrWhiteSpace(studentName)) studentName = user.FullName;
                if (string.IsNullOrWhiteSpace(universityName)) universityName = user.University;
            }
        }

        // 5. Create SimilarityCheck record
        var checkRecord = new SimilarityCheck
        {
            SubmittedTitle = title,
            SubmittedText = description,
            SubmittedKeywords = keywords,
            DepartmentName = departmentName ?? "Computer Science",
            StudentName = string.IsNullOrWhiteSpace(studentName) ? "Registered Student" : studentName.Trim(),
            UniversityName = string.IsNullOrWhiteSpace(universityName) ? "NUML Islamabad" : universityName.Trim(),
            SupervisorName = string.IsNullOrWhiteSpace(supervisorName) ? null : supervisorName.Trim(),
            CheckedAt = DateTime.UtcNow,
            TopScore = topScore,
            Verdict = verdict,
            StudentId = studentId,
            Matches = topMatches
        };

        _context.SimilarityChecks.Add(checkRecord);
        await _context.SaveChangesAsync();

        return checkRecord;
    }

    /// <summary>
    /// Computes the verdict based on configured thresholds.
    /// Below LowThreshold (20%) -> UNIQUE (Green)
    /// 20% to 50% -> SIMILAR (Amber)
    /// Above HighThreshold (50%) -> ALREADY DONE (Red)
    /// </summary>
    public string GetVerdict(double score, SimilaritySetting? settings = null)
    {
        settings ??= _context.SimilaritySettings.FirstOrDefault() ?? new SimilaritySetting();

        if (score < settings.LowThreshold)
            return "UNIQUE";
        else if (score <= settings.HighThreshold)
            return "SIMILAR";
        else
            return "ALREADY DONE";
    }

    /// <summary>
    /// Builds actionable suggestions for the student (Module 6 of Assignment Spec).
    /// Tailored according to the detected technical domain and verdict.
    /// </summary>
    public List<string> BuildSuggestions(SimilarityCheck check, List<SimilarityMatch> matches)
    {
        var suggestions = new List<string>();
        string domain = DetectDomain($"{check.SubmittedTitle} {check.SubmittedText} {check.SubmittedKeywords}");

        if (check.Verdict == "UNIQUE")
        {
            suggestions.Add("Novelty Confirmed: Your proposal demonstrates strong originality and does not duplicate archived department projects.");
            suggestions.Add("Lock in your concept: Reserve this idea now to generate an official NUML Reservation Token.");
            suggestions.Add("Next Steps: Prepare your formal Chapter 1 proposal documentation with your supervisor.");
            return suggestions;
        }

        // Domain-specific tailored advice for SIMILAR or ALREADY DONE
        if (domain.Contains("Vision"))
        {
            suggestions.Add("Methodological Pivot: Shift from standard 2D RGB models to edge-quantized architectures (YOLOv8-Nano, MobileNet) or thermal/depth sensing.");
            suggestions.Add("Scope Differentiation: Target non-standard real-world conditions such as low-light environments, occlusion handling, or anti-spoofing liveness verification.");
        }
        else if (domain.Contains("Language") || domain.Contains("NLP"))
        {
            suggestions.Add("Algorithmic Differentiation: Explore fine-tuning specialized domain SLMs (Small Language Models) or implementing hybrid Retrieval-Augmented Generation (RAG).");
            suggestions.Add("Target Scope: Adapt the system for regional low-resource languages (e.g. Urdu text parsing) or cross-lingual semantic matching.");
        }
        else if (domain.Contains("Blockchain"))
        {
            suggestions.Add("Architecture Shift: Implement zero-knowledge proofs (zk-SNARKs) for privacy-preserving verification rather than public transparent ledgers.");
            suggestions.Add("Scalability: Bridge validation across Layer-2 scaling solutions (Polygon/Arbitrum) with gas-optimized smart contracts.");
        }
        else if (domain.Contains("Cybersecurity"))
        {
            suggestions.Add("Threat Modeling: Pivot from generic packet anomaly detection to adversarial evasion defenses or automated smart contract vulnerability analysis.");
        }
        else if (domain.Contains("Healthcare"))
        {
            suggestions.Add("Clinical Integration: Focus on federated learning across multiple clinic nodes to preserve patient data confidentiality.");
        }
        else
        {
            suggestions.Add("Scope & Target Audience: Reframe the project to target a distinct user demographic (e.g. specialized campus research labs vs general classrooms).");
            suggestions.Add("Architectural Integration: Introduce real-time event-driven streaming, microservices orchestration, or offline-first PWA caching.");
        }

        // Department under-researched topic recommendations (Module 6)
        suggestions.Add("Recommended Under-Explored Department Areas: Consider integrating Edge AI Security, Decentralized Identity, or Autonomous Drone Logistics.");

        return suggestions;
    }

    /// <summary>
    /// Computes multi-tier similarity between a student submission and a candidate project or paper.
    /// Combines Title alignment (35%), Problem Statement / Abstract alignment (65%), and Domain Compatibility.
    /// </summary>
    public (double finalScore, double semanticScore, double keywordScore) CalculateMultiTierSimilarity(
        string queryTitle,
        string queryDescription,
        string? queryKeywords,
        string targetTitle,
        string? targetDescription,
        string? targetKeywords,
        string? targetEmbeddingJson,
        SimilaritySetting settings)
    {
        // 1. Title Similarity (Semantic + Keyword)
        string safeQueryTitle = queryTitle ?? "";
        string safeTargetTitle = targetTitle ?? "";
        double titleSemantic = _embeddingService.CosineSimilarity(safeQueryTitle, safeTargetTitle);
        double titleKeyword = _keywordService.JaccardScore(safeQueryTitle, safeTargetTitle);
        double titleScore = (settings.SemanticWeight * titleSemantic) + (settings.KeywordWeight * titleKeyword);

        // 2. Description / Problem Statement Similarity
        string cleanQueryDesc = string.IsNullOrWhiteSpace(queryDescription) ? safeQueryTitle : queryDescription;
        string cleanTargetDesc = string.IsNullOrWhiteSpace(targetDescription) ? safeTargetTitle : targetDescription;

        double descSemantic = _embeddingService.CosineSimilarityWithStored(
            _embeddingService.GetEmbedding(cleanQueryDesc),
            targetEmbeddingJson,
            cleanTargetDesc);

        double descKeyword = _keywordService.JaccardScore(cleanQueryDesc, cleanTargetDesc);
        double descScore = (settings.SemanticWeight * descSemantic) + (settings.KeywordWeight * descKeyword);

        // 3. Technical Domain Compatibility Factor
        string domainA = DetectDomain($"{queryTitle} {queryDescription} {queryKeywords}");
        string domainB = DetectDomain($"{targetTitle} {targetDescription} {targetKeywords}");

        double domainFactor = 1.0;
        if (domainA != domainB)
        {
            // If one is general software engineering and other is a sub-domain, mild adjustment
            if (domainA == "Software Engineering & Web Systems" || domainB == "Software Engineering & Web Systems")
            {
                domainFactor = 0.80;
            }
            else
            {
                // Completely distinct technical domains (e.g. Computer Vision vs NLP/Text, or Blockchain vs Agriculture)
                domainFactor = 0.45;
            }
        }

        // Weighted combination: Title (35%) + Description (65%)
        double combinedRawScore = (0.35 * titleScore) + (0.65 * descScore);
        double finalScore = Math.Round(Math.Clamp(combinedRawScore * domainFactor, 0.0, 100.0), 1);

        double avgSemantic = Math.Round((0.35 * titleSemantic) + (0.65 * descSemantic), 1);
        double avgKeyword = Math.Round((0.35 * titleKeyword) + (0.65 * descKeyword), 1);

        return (finalScore, avgSemantic, avgKeyword);
    }

    /// <summary>
    /// Detects the core technical domain of a project proposal based on academic terminology.
    /// </summary>
    public static string DetectDomain(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "General Computer Science";

        string lower = text.ToLowerInvariant();

        // 1. Computer Vision & Video
        if (Regex.IsMatch(lower, @"\b(face recognition|gaze|eye tracking|proctoring|opencv|yolo|yolov8|cnn|resnet|computer vision|camera|video stream|image classification|head pose)\b"))
            return "Computer Vision & Image Processing";

        // 2. NLP & Text Similarity
        if (Regex.IsMatch(lower, @"\b(similarity|plagiarism|embedding|embeddings|nlp|text matching|duplicate|jaccard|crossref|natural language|chatgpt|llm|corpus|semantic)\b"))
            return "Natural Language Processing & Information Retrieval";

        // 3. Blockchain & Crypto
        if (Regex.IsMatch(lower, @"\b(blockchain|smart contract|smart contracts|polygon|ethereum|solidity|decentralized|ledger|web3|crypto|token)\b"))
            return "Blockchain & Distributed Systems";

        // 4. Cybersecurity & Networks
        if (Regex.IsMatch(lower, @"\b(cybersecurity|intrusion detection|ids|ddos|malware|firewall|network traffic|packet|anomaly detection|penetration)\b"))
            return "Cybersecurity & Network Defense";

        // 5. Robotics & Drones
        if (Regex.IsMatch(lower, @"\b(uav|drone|robot|robotics|ros2|ros|path planning|autonomous|quadcopter|navigation)\b"))
            return "Robotics & Autonomous Systems";

        // 6. Healthcare & Medical
        if (Regex.IsMatch(lower, @"\b(hospital|patient|triage|clinic|medical|doctor|disease|cancer|health|appointment|mri|ct scan)\b"))
            return "Healthcare & Medical Informatics";

        // 7. IoT & Smart Systems
        if (Regex.IsMatch(lower, @"\b(traffic|congestion|vehicle|signal|iot|sensor|sensors|arduino|raspberry pi|microcontroller)\b"))
            return "Smart Systems & IoT";

        // 8. Agriculture
        if (Regex.IsMatch(lower, @"\b(crop|agriculture|leaf|plant|farming|pesticide)\b"))
            return "Smart Agriculture & AI";

        return "Software Engineering & Web Systems";
    }
}
