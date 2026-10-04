using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using UglyToad.PdfPig;

namespace FypMatchTracker.Services;

public class ExtractedDocumentInfo
{
    public string FileName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public bool IsSuccess { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Service to extract text, title, and meaningful proposal description from student and archive files
/// Supporting PDF (.pdf), Word Documents (.docx, .doc), Plain Text (.txt, .md), and CSV (.csv).
/// </summary>
public class DocumentExtractionService
{
    private readonly KeywordService _keywordService;

    public DocumentExtractionService(KeywordService keywordService)
    {
        _keywordService = keywordService;
    }

    /// <summary>
    /// Extracts text and structured metadata (Title, Clean Meaningful Description) from an uploaded file.
    public async Task<ExtractedDocumentInfo> ExtractInfoAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return new ExtractedDocumentInfo
            {
                IsSuccess = false,
                ErrorMessage = "The uploaded file is empty or not provided."
            };
        }

        // Limit file size to 30 MB to prevent server memory exhaustion
        if (file.Length > 30 * 1024 * 1024)
        {
            return new ExtractedDocumentInfo
            {
                FileName = file.FileName,
                IsSuccess = false,
                ErrorMessage = "File size exceeds 30 MB limit. Please upload a smaller document."
            };
        }

        var result = new ExtractedDocumentInfo
        {
            FileName = file.FileName
        };

        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        try
        {
            string extractedRaw = string.Empty;

            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                switch (ext)
                {
                    case ".pdf":
                        extractedRaw = ExtractFromPdf(memoryStream);
                        break;

                    case ".docx":
                        extractedRaw = ExtractFromDocx(memoryStream);
                        break;

                    case ".doc":
                        extractedRaw = ExtractFromLegacyDoc(memoryStream);
                        break;

                    case ".txt":
                    case ".md":
                    case ".json":
                    case ".csv":
                        using (var reader = new StreamReader(memoryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
                        {
                            extractedRaw = await reader.ReadToEndAsync();
                        }
                        break;

                    default:
                        using (var reader = new StreamReader(memoryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
                        {
                            extractedRaw = await reader.ReadToEndAsync();
                        }
                        break;
                }
            }

            result.RawText = extractedRaw?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(result.RawText))
            {
                result.IsSuccess = false;
                result.ErrorMessage = "Could not extract readable text from this file.";
                return result;
            }

            // Extract Clean Title and Meaningful Description
            result.Title = ExtractTitle(result.RawText, file.FileName);
            result.Description = ExtractCleanDescription(result.RawText);

            // Tools and frameworks left empty intentionally per user requirement
            result.Keywords = string.Empty;

            return result;
        }
        catch (Exception ex)
        {
            result.IsSuccess = false;
            result.ErrorMessage = $"Error extracting document: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Extracts text from PDF files using pure C# PdfPig.
    /// </summary>
    private string ExtractFromPdf(Stream pdfStream)
    {
        var sb = new StringBuilder();
        using (var document = PdfDocument.Open(pdfStream))
        {
            foreach (var page in document.GetPages())
            {
                if (!string.IsNullOrWhiteSpace(page.Text))
                {
                    sb.AppendLine(page.Text);
                }
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Extracts text from Microsoft Word (.docx) files using OpenXML zip structure.
    /// </summary>
    private string ExtractFromDocx(Stream docxStream)
    {
        var sb = new StringBuilder();
        using (var archive = new ZipArchive(docxStream, ZipArchiveMode.Read, leaveOpen: true))
        {
            var entry = archive.GetEntry("word/document.xml");
            if (entry != null)
            {
                using var entryStream = entry.Open();
                var xDoc = XDocument.Load(entryStream);
                XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

                var paragraphs = xDoc.Descendants(w + "p");
                foreach (var p in paragraphs)
                {
                    var textElements = p.Descendants(w + "t").Select(t => t.Value);
                    var paragraphText = string.Join("", textElements).Trim();
                    if (!string.IsNullOrWhiteSpace(paragraphText))
                    {
                        sb.AppendLine(paragraphText);
                    }
                }
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Text extraction for legacy binary Word (.doc) files.
    /// </summary>
    private string ExtractFromLegacyDoc(Stream docStream)
    {
        var sb = new StringBuilder();
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            docStream.CopyTo(ms);
            bytes = ms.ToArray();
        }

        var currentRun = new StringBuilder();
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            if (b >= 32 && b <= 126)
            {
                currentRun.Append((char)b);
            }
            else if (b == '\n' || b == '\r')
            {
                if (currentRun.Length > 3)
                {
                    sb.AppendLine(currentRun.ToString());
                }
                currentRun.Clear();
            }
            else
            {
                if (currentRun.Length > 4)
                {
                    sb.Append(currentRun.ToString()).Append(' ');
                }
                currentRun.Clear();
            }
        }

        if (currentRun.Length > 4)
        {
            sb.Append(currentRun.ToString());
        }

        return sb.ToString();
    }

    /// <summary>
    /// Accurately extracts the project title from text, recognizing labels like 'PROJECT' or 'P R OJE CT'
    /// and picking the actual title on the next line (e.g., 'FYP MATCH TRACKER').
    /// </summary>
    public string ExtractTitle(string fullText, string fileName)
    {
        var rawLines = fullText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                               .Select(l => l.Trim())
                               .Where(l => l.Length > 0)
                               .ToList();

        // Regex for labels indicating that the NEXT line is the project title
        var titleLabelRegex = new Regex(@"^(?:P\s*R\s*O\s*J\s*E\s*C\s*T|PROJECT\s+TITLE|TITLE|PROPOSED\s+PROJECT|PROPOSAL\s+TITLE)\s*[:\-]?\s*$", RegexOptions.IgnoreCase);

        // Regex for inline title labels e.g. "Project Title: FYP Match Tracker"
        var inlineLabelRegex = new Regex(@"^(?:P\s*R\s*O\s*J\s*E\s*C\s*T|PROJECT\s+TITLE|TITLE|PROPOSED\s+PROJECT|PROPOSAL\s+TITLE)\s*[:\-]\s*(.+)$", RegexOptions.IgnoreCase);

        for (int i = 0; i < rawLines.Count; i++)
        {
            string line = rawLines[i];

            // 1. Check if line has inline title
            var inlineMatch = inlineLabelRegex.Match(line);
            if (inlineMatch.Success && inlineMatch.Groups.Count > 1)
            {
                string candidate = CleanTitleString(inlineMatch.Groups[1].Value);
                if (IsValidTitle(candidate)) return candidate;
            }

            // 2. Check if line is a title label like "PROJECT" or "P R OJE CT", where title is on the NEXT line
            if (titleLabelRegex.IsMatch(line))
            {
                for (int j = i + 1; j < Math.Min(i + 5, rawLines.Count); j++)
                {
                    string nextCandidate = CleanTitleString(rawLines[j]);
                    if (IsValidTitle(nextCandidate) && !IsIgnoredHeader(nextCandidate))
                    {
                        return nextCandidate;
                    }
                }
            }
        }

        // 3. Look for title appearing right before "System Description" or "Abstract"
        for (int i = 0; i < rawLines.Count; i++)
        {
            string line = rawLines[i];
            if (Regex.IsMatch(line, @"^(?:\d+\.|\d+\.\d+)?\s*(?:System\s+Description|Project\s+Description|Abstract|Executive\s+Summary)", RegexOptions.IgnoreCase))
            {
                // Check up to 3 lines above this section header
                for (int j = i - 1; j >= Math.Max(0, i - 4); j--)
                {
                    string candidate = CleanTitleString(rawLines[j]);
                    if (IsValidTitle(candidate) && !IsIgnoredHeader(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }

        // 4. Scan early lines, skipping university and assignment boilerplate
        foreach (var line in rawLines.Take(25))
        {
            if (IsIgnoredHeader(line)) continue;

            string candidate = CleanTitleString(line);
            if (IsValidTitle(candidate))
            {
                return candidate;
            }
        }

        // 5. Fallback to clean file name without extension
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        baseName = Regex.Replace(baseName, @"[_\-]+", " ").Trim();
        if (!string.IsNullOrWhiteSpace(baseName) && !baseName.Equals("project", StringComparison.OrdinalIgnoreCase) && !baseName.Equals("document", StringComparison.OrdinalIgnoreCase))
        {
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(baseName);
        }

        return "Final Year Project Proposal";
    }

    /// <summary>
    /// Extracts a clean, coherent description from document text without headers, page numbers, or tables.
    /// Produces a summary that reflects the exact core meaning of the project proposal.
    /// </summary>
    public string ExtractCleanDescription(string fullText)
    {
        if (string.IsNullOrWhiteSpace(fullText))
            return string.Empty;

        // 1. Remove running headers (e.g. "... | NUML Islamabad") and page numbers directly across text
        string text = Regex.Replace(fullText, @"[^\r\n\.]*?\|[^\r\n\.]*?\|[^\r\n\.]*", " ");
        text = Regex.Replace(text, @"Page\s+\d+(\s+of\s+\d+)?", " ", RegexOptions.IgnoreCase);

        // 2. Primary: Match standard proposal sections: "System Description", "Project Description", "Proposal Abstract", "Abstract", "Executive Summary", "Problem Statement"
        var match = Regex.Match(text, @"(?:System\s+Description|Project\s+Description|Proposal\s+Abstract|Abstract|Executive\s+Summary|Problem\s+Statement)[\s:\-]*(.*?)(?=Highest\s+similarity|1\.3|2\.|\bFunctions\s+and\s+Features\b|\bDiagrams\b|\bInspiration\b|\Z)", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (match.Success && match.Groups.Count > 1 && match.Groups[1].Value.Trim().Length > 30)
        {
            string desc = match.Groups[1].Value;

            // Remove specification tables e.g. "User What they do..."
            desc = Regex.Replace(desc, @"User\s+What they do[\s\S]*?(?=1\.2|How it works|\Z)", " ", RegexOptions.IgnoreCase);

            // Clean subheaders
            desc = Regex.Replace(desc, @"(?:\d+\.\d+)?\s*Purpose\s+and\s+target\s+users", " Purpose: ", RegexOptions.IgnoreCase);
            desc = Regex.Replace(desc, @"(?:\d+\.\d+)?\s*How\s+it\s+works", " How it works: ", RegexOptions.IgnoreCase);
            desc = Regex.Replace(desc, @"[•\*\-]", " ");

            // Clean spaces
            desc = Regex.Replace(desc, @"\s+", " ").Trim();

            if (desc.Length > 30)
            {
                return TruncateToWordCount(desc, 400);
            }
        }

        // 3. Secondary: Match Overview, Introduction, or Summary
        var match2 = Regex.Match(text, @"(?:Introduction|Project\s+Overview|System\s+Overview|Overview|Summary)[\s:\-]*(.*?)(?=Methodology|Architecture|Diagrams|2\.|\bFigure\b|\Z)", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (match2.Success && match2.Groups.Count > 1 && match2.Groups[1].Value.Trim().Length > 30)
        {
            string desc = match2.Groups[1].Value;
            desc = Regex.Replace(desc, @"[•\*\-]", " ");
            desc = Regex.Replace(desc, @"\s+", " ").Trim();
            if (desc.Length > 30)
            {
                return TruncateToWordCount(desc, 400);
            }
        }

        // 4. Fallback: Parse paragraphs from cleaned text
        var rawLines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                           .Select(l => l.Trim())
                           .Where(l => l.Length > 25 && !IsIgnoredHeader(l) && !IsTableArtifact(l))
                           .Take(12);

        string fallback = string.Join(" ", rawLines);
        fallback = Regex.Replace(fallback, @"\s+", " ").Trim();
        if (string.IsNullOrWhiteSpace(fallback))
        {
            fallback = Regex.Replace(text, @"\s+", " ").Trim();
        }

        return TruncateToWordCount(fallback, 350);
    }

    private bool IsIgnoredHeader(string line)
    {
        return line.StartsWith("National University", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("NUML", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Department of", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Course:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Class:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Submitted to:", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Submitted by", StringComparison.OrdinalIgnoreCase) ||
               line.StartsWith("Date:", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(line, @"^ASSIGNMENT\s*(?:NO\.?\s*\d+)?$", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(line, @"^Design\s+an\s+Enterprise", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(line, @"^(?:P\s*R\s*O\s*J\s*E\s*C\s*T|PROJECT)$", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(line, @"^(?:Fall|Spring)\s*\d{4}", RegexOptions.IgnoreCase);
    }

    private bool IsRunningHeaderOrFooter(string line)
    {
        // Matches e.g. "FYP Match Tracker | Advance Programming (Theory) | NUML Islamabad"
        if (line.Contains("|") && (line.Contains("NUML", StringComparison.OrdinalIgnoreCase) || 
                                   line.Contains("Theory", StringComparison.OrdinalIgnoreCase) || 
                                   line.Contains("Islamabad", StringComparison.OrdinalIgnoreCase) ||
                                   line.Contains("Programming", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Matches "Page 2 of 7", "Page 1", etc.
        if (Regex.IsMatch(line, @"^Page\s+\d+(\s+of\s+\d+)?$", RegexOptions.IgnoreCase))
        {
            return true;
        }

        return false;
    }

    private bool IsTableArtifact(string line)
    {
        // Filter out table rows and column headers from specification documents
        return Regex.IsMatch(line, @"^User\s+What they do", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(line, @"^Highest similarity\s+Verdict", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(line, @"^Score\s+What it checks", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(line, @"^Below 20%\s+UNIQUE", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(line, @"^20% to 50%\s+SIMILAR", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(line, @"^Above 50%\s+ALREADY DONE", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(line, @"^Final similarity\s*=", RegexOptions.IgnoreCase) ||
               Regex.IsMatch(line, @"^Figure\s+\d+:", RegexOptions.IgnoreCase);
    }

    private bool IsValidTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        if (title.Length < 3 || title.Length > 120) return false;

        // Skip standalone generic label words
        string upper = title.Trim().ToUpperInvariant();
        if (upper == "PROJECT" || upper == "PROPOSAL" || upper == "ASSIGNMENT" || upper == "REPORT" || upper == "FYP")
        {
            return false;
        }

        return true;
    }

    private string CleanTitleString(string raw)
    {
        string cleaned = raw.Trim().Trim('"', '\'', ':', '-', '•');
        // Remove duplicate spaces
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        return cleaned;
    }

    private string TruncateToWordCount(string text, int maxWords)
    {
        var words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= maxWords)
        {
            return text.Trim();
        }

        return string.Join(" ", words.Take(maxWords)).Trim() + "...";
    }
}
