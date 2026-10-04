using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FypMatchTracker.Data;
using FypMatchTracker.Models;
using FypMatchTracker.Services;

namespace FypMatchTracker.Controllers;

/// <summary>
/// Controller for FYP Coordinator operations:
/// - Department Analytics Dashboard (Chart.js feeds)
/// - FYP Archive Library (Search, Filter, Pagination, CSV Bulk Import)
/// - Algorithm Sensitivity Settings (Threshold & Weight sliders)
/// </summary>
[Authorize(Roles = "Coordinator")]
public class CoordinatorController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly KeywordService _keywordService;
    private readonly EmbeddingService _embeddingService;
    private readonly DocumentExtractionService _documentExtractionService;

    public CoordinatorController(
        ApplicationDbContext context,
        KeywordService keywordService,
        EmbeddingService embeddingService,
        DocumentExtractionService documentExtractionService)
    {
        _context = context;
        _keywordService = keywordService;
        _embeddingService = embeddingService;
        _documentExtractionService = documentExtractionService;
    }

    // ==========================================
    // GET: /Coordinator/Dashboard
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        // 1. KPI Aggregates from real Database
        int totalProjects = await _context.FypProjects.CountAsync();
        int totalChecks = await _context.SimilarityChecks.CountAsync();
        int highRiskChecks = await _context.SimilarityChecks.CountAsync(c => c.Verdict == "ALREADY DONE");
        double avgScore = totalChecks > 0
            ? await _context.SimilarityChecks.AverageAsync(c => c.TopScore)
            : 0.0;

        ViewBag.TotalProjects = totalProjects;
        ViewBag.TotalChecks = totalChecks;
        ViewBag.HighRiskChecks = highRiskChecks;
        ViewBag.AvgScore = Math.Round(avgScore, 1);

        // 2. Chart 1: Projects Per Year (Archive Trends)
        var projectsByYear = await _context.FypProjects
            .GroupBy(p => p.Year)
            .OrderBy(g => g.Key)
            .Select(g => new { Year = g.Key, Count = g.Count() })
            .ToListAsync();

        ViewBag.YearLabels = projectsByYear.Select(x => x.Year.ToString()).ToList();
        ViewBag.YearCounts = projectsByYear.Select(x => x.Count).ToList();

        // 3. Chart 2: Verdict Breakdown (UNIQUE=Green, SIMILAR=Yellow, ALREADY DONE=Red)
        int greenCount = await _context.SimilarityChecks.CountAsync(c => c.Verdict == "UNIQUE");
        int yellowCount = await _context.SimilarityChecks.CountAsync(c => c.Verdict == "SIMILAR");
        int redCount = await _context.SimilarityChecks.CountAsync(c => c.Verdict == "ALREADY DONE");

        ViewBag.VerdictCounts = new List<int> { greenCount, yellowCount, redCount };

        // 4. Top Keywords from Archive
        var allProjects = await _context.FypProjects
            .Select(p => p.Keywords)
            .ToListAsync();

        var keywordFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var kwStr in allProjects)
        {
            if (string.IsNullOrWhiteSpace(kwStr)) continue;
            var tokens = kwStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                keywordFreq[token] = keywordFreq.GetValueOrDefault(token, 0) + 1;
            }
        }

        var topKeywords = keywordFreq
            .OrderByDescending(kv => kv.Value)
            .Take(8)
            .Select(kv => new { Keyword = kv.Key, Count = kv.Value })
            .ToList();

        ViewBag.TopKeywords = topKeywords;

        // 5. Supervisor Allocation Breakdown (Faculty Load Balancing)
        var supervisorLoad = await _context.FypProjects
            .Where(p => !string.IsNullOrEmpty(p.Supervisor))
            .GroupBy(p => p.Supervisor!)
            .Select(g => new { Supervisor = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(6)
            .ToListAsync();

        ViewBag.SupervisorLoad = supervisorLoad;

        // 6. Recent Checks Table
        var recentChecks = await _context.SimilarityChecks
            .Include(c => c.Student)
            .OrderByDescending(c => c.CheckedAt)
            .Take(6)
            .ToListAsync();

        return View(recentChecks);
    }

    // ==========================================
    // GET: /Coordinator/Library
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> Library(string? query, int? departmentId, int? year, string? status, int page = 1)
    {
        const int pageSize = 10;
        var projectsQuery = _context.FypProjects
            .Include(p => p.Department)
            .Include(p => p.Owner)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLower();
            projectsQuery = projectsQuery.Where(p =>
                p.Title.ToLower().Contains(q) ||
                (p.StudentName != null && p.StudentName.ToLower().Contains(q)) ||
                (p.UniversityName != null && p.UniversityName.ToLower().Contains(q)) ||
                (p.Keywords != null && p.Keywords.ToLower().Contains(q)) ||
                (p.Supervisor != null && p.Supervisor.ToLower().Contains(q)));
        }

        if (departmentId.HasValue && departmentId.Value > 0)
        {
            projectsQuery = projectsQuery.Where(p => p.DepartmentId == departmentId.Value);
        }

        if (year.HasValue && year.Value > 0)
        {
            projectsQuery = projectsQuery.Where(p => p.Year == year.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            projectsQuery = projectsQuery.Where(p => p.Status == status);
        }

        page = Math.Max(1, page);
        int totalCount = await projectsQuery.CountAsync();
        var projects = await projectsQuery
            .OrderByDescending(p => p.Year)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
        ViewBag.Years = await _context.FypProjects.Select(p => p.Year).Distinct().OrderByDescending(y => y).ToListAsync();
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        ViewBag.TotalItems = totalCount;
        ViewBag.Query = query;
        ViewBag.DepartmentId = departmentId;
        ViewBag.Year = year;
        ViewBag.Status = status;

        return View(projects);
    }

    // ==========================================
    // POST: /Coordinator/AddProject
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProject(string title, string description, int departmentId, int year, string? supervisor, string? keywords, string? studentName, string? universityName)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
        {
            TempData["ErrorMessage"] = "Project Title and Description are required.";
            return RedirectToAction("Library");
        }

        // Auto-extract keywords if none provided
        if (string.IsNullOrWhiteSpace(keywords))
        {
            var extracted = _keywordService.ExtractTopKeywords($"{title} {description}", 6);
            keywords = string.Join(", ", extracted);
        }

        // Generate embedding vector
        var embeddingJson = _embeddingService.GetEmbeddingJson($"{title}. {description}");

        var project = new FypProject
        {
            Title = title.Trim(),
            Description = description.Trim(),
            StudentName = studentName?.Trim(),
            UniversityName = string.IsNullOrWhiteSpace(universityName) ? "NUML Islamabad" : universityName.Trim(),
            DepartmentId = departmentId,
            Year = year > 1900 ? year : DateTime.UtcNow.Year,
            Supervisor = supervisor?.Trim(),
            Keywords = keywords?.Trim(),
            EmbeddingJson = embeddingJson,
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        };

        _context.FypProjects.Add(project);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Project '{project.Title}' was added to the archive successfully.";
        return RedirectToAction("Library");
    }

    // ==========================================
    // POST: /Coordinator/BulkImportFiles or /Coordinator/BulkImportCsv
    // Supports PDF, Word (.docx, .doc), Text (.txt), and CSV (.csv)
    // ==========================================
    [HttpPost]
    [ActionName("BulkImportFiles")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkImportFiles(List<IFormFile>? files, IFormFile? csvFile, int defaultDepartmentId)
    {
        var allFiles = new List<IFormFile>();
        if (files != null && files.Count > 0) allFiles.AddRange(files);
        if (csvFile != null && csvFile.Length > 0) allFiles.Add(csvFile);

        if (allFiles.Count == 0)
        {
            TempData["ErrorMessage"] = "Please select at least one document or CSV file (PDF, Word, TXT, or CSV).";
            return RedirectToAction("Library");
        }

        int importedCount = 0;
        int errorCount = 0;

        foreach (var file in allFiles)
        {
            if (file == null || file.Length == 0) continue;

            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (ext == ".csv")
            {
                try
                {
                    using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
                    string? headerLine = await reader.ReadLineAsync(); // Read headers

                    int titleIdx = 0, descIdx = 1, yearIdx = 2, supIdx = 3, studentIdx = -1, uniIdx = -1;
                    if (!string.IsNullOrWhiteSpace(headerLine))
                    {
                        var headers = headerLine.Split(',').Select(h => h.Trim().Trim('"').ToLowerInvariant()).ToList();
                        int foundTitle = headers.FindIndex(h => h.Contains("title"));
                        int foundDesc = headers.FindIndex(h => h.Contains("desc") || h.Contains("abstract"));
                        int foundYear = headers.FindIndex(h => h.Contains("year"));
                        int foundSup = headers.FindIndex(h => h.Contains("supervisor") || h.Contains("advisor"));
                        int foundStudent = headers.FindIndex(h => h.Contains("student") || h.Contains("author") || h.Contains("member"));
                        int foundUni = headers.FindIndex(h => h.Contains("uni") || h.Contains("institute") || h.Contains("college"));

                        if (foundTitle >= 0) titleIdx = foundTitle;
                        if (foundDesc >= 0) descIdx = foundDesc;
                        if (foundYear >= 0) yearIdx = foundYear;
                        if (foundSup >= 0) supIdx = foundSup;
                        if (foundStudent >= 0) studentIdx = foundStudent;
                        if (foundUni >= 0) uniIdx = foundUni;
                    }

                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var cols = line.Split(',');
                        if (cols.Length <= Math.Max(titleIdx, descIdx)) continue;

                        string title = cols[titleIdx].Trim().Trim('"');
                        string description = cols[descIdx].Trim().Trim('"');
                        int year = yearIdx >= 0 && cols.Length > yearIdx && int.TryParse(cols[yearIdx].Trim().Trim('"'), out int y) ? y : DateTime.UtcNow.Year;
                        string supervisor = supIdx >= 0 && cols.Length > supIdx && !string.IsNullOrWhiteSpace(cols[supIdx]) ? cols[supIdx].Trim().Trim('"') : "Faculty Advisor";
                        string student = studentIdx >= 0 && cols.Length > studentIdx && !string.IsNullOrWhiteSpace(cols[studentIdx]) ? cols[studentIdx].Trim().Trim('"') : "Department Student";
                        string uni = uniIdx >= 0 && cols.Length > uniIdx && !string.IsNullOrWhiteSpace(cols[uniIdx]) ? cols[uniIdx].Trim().Trim('"') : "NUML Islamabad";

                        if (string.IsNullOrWhiteSpace(title)) continue;

                        var extractedKeywords = _keywordService.ExtractTopKeywords($"{title} {description}", 6);
                        var embeddingJson = _embeddingService.GetEmbeddingJson($"{title}. {description}");

                        var project = new FypProject
                        {
                            Title = title,
                            Description = description,
                            StudentName = student,
                            UniversityName = uni,
                            DepartmentId = defaultDepartmentId > 0 ? defaultDepartmentId : 1,
                            Year = year,
                            Supervisor = supervisor,
                            Keywords = string.Join(", ", extractedKeywords),
                            EmbeddingJson = embeddingJson,
                            Status = "Completed",
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.FypProjects.Add(project);
                        importedCount++;
                    }
                }
                catch
                {
                    errorCount++;
                }
            }
            else if (ext == ".pdf" || ext == ".docx" || ext == ".doc" || ext == ".txt")
            {
                try
                {
                    var docInfo = await _documentExtractionService.ExtractInfoAsync(file);
                    if (docInfo.IsSuccess && !string.IsNullOrWhiteSpace(docInfo.Description))
                    {
                        var embeddingJson = _embeddingService.GetEmbeddingJson($"{docInfo.Title}. {docInfo.Description}");

                        var project = new FypProject
                        {
                            Title = docInfo.Title,
                            Description = docInfo.Description,
                            DepartmentId = defaultDepartmentId > 0 ? defaultDepartmentId : 1,
                            Year = DateTime.UtcNow.Year,
                            Supervisor = "Faculty Advisor",
                            Keywords = docInfo.Keywords,
                            EmbeddingJson = embeddingJson,
                            Status = "Completed",
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.FypProjects.Add(project);
                        importedCount++;
                    }
                    else
                    {
                        errorCount++;
                    }
                }
                catch
                {
                    errorCount++;
                }
            }
        }

        if (importedCount > 0)
        {
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Successfully imported and indexed {importedCount} project(s) into the archive from your documents.";
        }
        else
        {
            TempData["ErrorMessage"] = "Could not import any valid projects from the selected file(s). Please verify file formats (PDF, Word DOCX/DOC, TXT, or CSV).";
        }

        return RedirectToAction("Library");
    }

    [HttpPost]
    [ActionName("BulkImportCsv")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> BulkImportCsv(List<IFormFile>? files, IFormFile? csvFile, int defaultDepartmentId)
    {
        return BulkImportFiles(files, csvFile, defaultDepartmentId);
    }

    // ==========================================
    // GET: /Coordinator/Settings
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> Settings()
    {
        var settings = await _context.SimilaritySettings.OrderBy(s => s.Id).FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new SimilaritySetting();
            _context.SimilaritySettings.Add(settings);
            await _context.SaveChangesAsync();
        }

        return View(settings);
    }

    // ==========================================
    // POST: /Coordinator/Settings
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(double lowThreshold, double highThreshold, double semanticWeight, double keywordWeight)
    {
        var settings = await _context.SimilaritySettings.OrderBy(s => s.Id).FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new SimilaritySetting();
            _context.SimilaritySettings.Add(settings);
        }

        settings.LowThreshold = Math.Clamp(lowThreshold, 0, 100);
        settings.HighThreshold = Math.Clamp(highThreshold, 0, 100);

        // Ensure Low < High
        if (settings.LowThreshold >= settings.HighThreshold)
        {
            TempData["ErrorMessage"] = "Low threshold must be strictly less than high threshold.";
            return View(settings);
        }

        // Normalize weights to sum to 1.0 (e.g. 0.70 and 0.30)
        double totalWeight = semanticWeight + keywordWeight;
        if (totalWeight <= 0) totalWeight = 100;
        settings.SemanticWeight = Math.Round(semanticWeight / totalWeight, 2);
        settings.KeywordWeight = Math.Round(1.0 - settings.SemanticWeight, 2);
        settings.UpdatedAt = DateTime.UtcNow;

        TempData["SuccessMessage"] = "Threshold settings updated successfully! All future similarity checks will use these new criteria.";
        return RedirectToAction("Settings");
    }

    // ==========================================
    // POST: /Coordinator/ApproveProject/{id}
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveProject(int id)
    {
        var proj = await _context.FypProjects.FindAsync(id);
        if (proj != null)
        {
            proj.Status = "Approved";
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Project '{proj.Title}' has been approved by Coordinator.";
        }
        return RedirectToAction("Library");
    }

    // ==========================================
    // POST: /Coordinator/DeleteProject/{id}
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProject(int id)
    {
        var proj = await _context.FypProjects.FindAsync(id);
        if (proj != null)
        {
            string title = proj.Title;
            _context.FypProjects.Remove(proj);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Project '{title}' was deleted from the archive.";
        }
        return RedirectToAction("Library");
    }

    // ==========================================
    // GET: /Coordinator/ExportArchiveCsv
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> ExportArchiveCsv()
    {
        var projects = await _context.FypProjects
            .Include(p => p.Department)
            .OrderByDescending(p => p.Year)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("ID,Title,Student,University,Department,Year,Status,Supervisor,ReservationToken,Keywords,Description");

        foreach (var p in projects)
        {
            string cleanTitle = p.Title.Replace("\"", "\"\"");
            string cleanStudent = (p.StudentName ?? "Department Student").Replace("\"", "\"\"");
            string cleanUni = (p.UniversityName ?? "NUML Islamabad").Replace("\"", "\"\"");
            string cleanDesc = (p.Description ?? "").Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ");
            string cleanKw = (p.Keywords ?? "").Replace("\"", "\"\"");
            string cleanSup = (p.Supervisor ?? "").Replace("\"", "\"\"");
            string dept = (p.Department?.Name ?? "").Replace("\"", "\"\"");

            sb.AppendLine($"\"{p.Id}\",\"{cleanTitle}\",\"{cleanStudent}\",\"{cleanUni}\",\"{dept}\",\"{p.Year}\",\"{p.Status}\",\"{cleanSup}\",\"{p.ReservationToken}\",\"{cleanKw}\",\"{cleanDesc}\"");
        }

        byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"numl_fyp_archive_{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
