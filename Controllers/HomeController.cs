using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FypMatchTracker.Data;
using FypMatchTracker.Models;
using FypMatchTracker.Services;

namespace FypMatchTracker.Controllers;

/// <summary>
/// Controller for public and student workflows:
/// - Splash screen
/// - Idea submission form
/// - Similarity check execution
/// - Detailed Similarity Report
/// - Idea Reservation
/// </summary>
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly SimilarityService _similarityService;
    private readonly EmbeddingService _embeddingService;
    private readonly DocumentExtractionService _documentExtractionService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        ApplicationDbContext context,
        SimilarityService similarityService,
        EmbeddingService embeddingService,
        DocumentExtractionService documentExtractionService,
        ILogger<HomeController> logger)
    {
        _context = context;
        _similarityService = similarityService;
        _embeddingService = embeddingService;
        _documentExtractionService = documentExtractionService;
        _logger = logger;
    }

    // ==========================================
    // GET: / (Splash Screen)
    // ==========================================
    [HttpGet]
    public IActionResult Splash()
    {
        return View();
    }

    // ==========================================
    // GET: /Home/Index (Idea Submission Form)
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewBag.Departments = await _context.Departments.OrderBy(d => d.Name).ToListAsync();
        return View();
    }

    // ==========================================
    // GET: /Home/ApiDocs (Developer Web API Explorer)
    // ==========================================
    [HttpGet]
    public IActionResult ApiDocs()
    {
        return View();
    }

    // ==========================================
    // POST: /Home/ExtractDocument (AJAX)
    // Parses student PDF, Word (.docx, .doc), or Text files
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExtractDocument(IFormFile? documentFile)
    {
        if (documentFile == null || documentFile.Length == 0)
        {
            return Json(new { success = false, message = "Please select a valid document file." });
        }

        try
        {
            var info = await _documentExtractionService.ExtractInfoAsync(documentFile);
            if (!info.IsSuccess)
            {
                return Json(new { success = false, message = info.ErrorMessage ?? "Could not extract text from this document." });
            }

            return Json(new
            {
                success = true,
                fileName = documentFile.FileName,
                title = info.Title,
                description = info.Description,
                keywords = info.Keywords
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract text from proposal document.");
            return Json(new { success = false, message = $"Failed to parse document: {ex.Message}" });
        }
    }

    // ==========================================
    // POST: /Home/CheckIdea
    // ==========================================
    [HttpPost]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIdea(
        string? title, 
        string? description, 
        int departmentId, 
        string? keywords, 
        string? supervisor, 
        string? studentName, 
        string? universityName, 
        IFormFile? documentFile)
    {
        // If file provided and fields are missing, extract from document
        if (documentFile != null && documentFile.Length > 0 && (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description)))
        {
            var docInfo = await _documentExtractionService.ExtractInfoAsync(documentFile);
            if (docInfo.IsSuccess)
            {
                if (string.IsNullOrWhiteSpace(title)) title = docInfo.Title;
                if (string.IsNullOrWhiteSpace(description)) description = docInfo.Description;
                if (string.IsNullOrWhiteSpace(keywords)) keywords = docInfo.Keywords;
            }
        }

        if (departmentId <= 0)
        {
            TempData["ErrorMessage"] = "Please select your Academic Department before submitting.";
            return RedirectToAction("Index");
        }

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
        {
            TempData["ErrorMessage"] = "Please provide both project title and a description (or upload a proposal document).";
            return RedirectToAction("Index");
        }

        int? studentId = null;
        string resolvedStudentName = "Guest Student";
        string resolvedUniversityName = "NUML Islamabad";

        // Check if user is logged in
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(idClaim, out int parsedId) && parsedId > 0)
            {
                studentId = parsedId;
                var studentUser = await _context.Users.FindAsync(studentId.Value);
                if (studentUser != null)
                {
                    resolvedStudentName = studentUser.FullName;
                    resolvedUniversityName = studentUser.University ?? "NUML Islamabad";
                }
            }
            else
            {
                // Guest session with custom or default name
                if (!string.IsNullOrWhiteSpace(studentName)) resolvedStudentName = studentName.Trim();
                else resolvedStudentName = User.Identity.Name ?? "Guest Student";

                if (!string.IsNullOrWhiteSpace(universityName)) resolvedUniversityName = universityName.Trim();
                else resolvedUniversityName = User.FindFirst("University")?.Value ?? "NUML Islamabad";
            }
        }
        else
        {
            // Unauthenticated Guest visitor
            if (!string.IsNullOrWhiteSpace(studentName)) resolvedStudentName = studentName.Trim();
            if (!string.IsNullOrWhiteSpace(universityName)) resolvedUniversityName = universityName.Trim();
        }

        var dept = await _context.Departments.FindAsync(departmentId);
        string deptName = dept?.Name ?? "Computer Science";

        try
        {
            var checkResult = await _similarityService.CheckIdeaAsync(
                title.Trim(),
                description.Trim(),
                keywords?.Trim(),
                deptName,
                studentId,
                resolvedStudentName,
                resolvedUniversityName,
                supervisor?.Trim()
            );

            return RedirectToAction("Report", new { id = checkResult.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while performing similarity check.");
            TempData["ErrorMessage"] = "An error occurred while evaluating similarity. Please try again.";
            return RedirectToAction("Index");
        }
    }

    // ==========================================
    // GET: /Home/Report/{id}
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> Report(int id)
    {
        var check = await _context.SimilarityChecks
            .Include(c => c.Student)
            .Include(c => c.Matches)
                .ThenInclude(m => m.FypProject)
                    .ThenInclude(p => p!.Department)
            .Include(c => c.Matches)
                .ThenInclude(m => m.ExternalPaper)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (check == null)
        {
            TempData["ErrorMessage"] = "Report not found or has expired.";
            return RedirectToAction("Index");
        }

        // Fetch current active threshold settings for display
        var settings = await _context.SimilaritySettings.OrderBy(s => s.Id).FirstOrDefaultAsync()
            ?? new SimilaritySetting();

        ViewBag.Settings = settings;

        // Separate matches into Internal Department FYPs and External Research Papers (strictly >= 10.0%)
        var internalMatches = check.Matches
            .Where(m => m.FypProject != null && m.FinalScore >= 10.0)
            .OrderByDescending(m => m.FinalScore)
            .ToList();

        var externalMatches = check.Matches
            .Where(m => m.ExternalPaper != null && m.FinalScore >= 10.0)
            .OrderByDescending(m => m.FinalScore)
            .ToList();

        ViewBag.InternalMatches = internalMatches;
        ViewBag.ExternalMatches = externalMatches;
        ViewBag.TopMatch = check.Matches.Where(m => m.FinalScore >= 10.0).OrderByDescending(m => m.FinalScore).FirstOrDefault();
        ViewBag.Suggestions = _similarityService.BuildSuggestions(check, check.Matches.ToList());

        // Check if idea is already reserved or approved
        var reservedProject = await _context.FypProjects
            .Include(p => p.Department)
            .Include(p => p.Owner)
            .FirstOrDefaultAsync(p => p.Title.ToLower() == check.SubmittedTitle.ToLower() && (p.Status == "Reserved" || p.Status == "Approved"));

        ViewBag.ReservedProject = reservedProject;
        ViewBag.IsAlreadyReserved = reservedProject != null;

        return View(check);
    }

    // ==========================================
    // POST: /Home/ReserveIdea
    // ==========================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReserveIdea(int checkId, string? supervisor)
    {
        if (User.Identity == null || !User.Identity.IsAuthenticated || !User.IsInRole("Student"))
        {
            TempData["ErrorMessage"] = "Please sign in or register with your institutional student account to officially reserve this FYP proposal.";
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Report", "Home", new { id = checkId }) });
        }

        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idClaim, out int studentId))
        {
            TempData["ErrorMessage"] = "Invalid user session.";
            return RedirectToAction("Login", "Account");
        }

        var check = await _context.SimilarityChecks.FirstOrDefaultAsync(c => c.Id == checkId);
        if (check == null)
        {
            TempData["ErrorMessage"] = "Submission check not found.";
            return RedirectToAction("Index");
        }

        // Verify verdict is UNIQUE (Green / Low Similarity)
        if (check.Verdict != "UNIQUE")
        {
            TempData["ErrorMessage"] = "Only ideas with low similarity (UNIQUE verdict) can be reserved directly.";
            return RedirectToAction("Report", new { id = checkId });
        }

        // Check if student already has a reserved or active project
        var existing = await _context.FypProjects
            .FirstOrDefaultAsync(p => p.OwnerId == studentId && (p.Status == "Reserved" || p.Status == "Approved"));

        if (existing != null)
        {
            TempData["ErrorMessage"] = $"You already have an active project reservation ({existing.ReservationToken}).";
            return RedirectToAction("Report", new { id = checkId });
        }

        // Find department id matching check.DepartmentName or default 1
        var dept = await _context.Departments
            .FirstOrDefaultAsync(d => d.Name == check.DepartmentName)
            ?? await _context.Departments.FirstOrDefaultAsync();

        // Generate unique reservation token format: NUML-FYP-{Year}-{Random4Digits}
        var random = new Random();
        string token = $"NUML-FYP-{DateTime.UtcNow.Year}-{random.Next(1000, 9999)}";

        // Pre-compute local embedding for fast future similarity checks
        string projectFullText = $"{check.SubmittedTitle}. {check.SubmittedText}";
        if (!string.IsNullOrWhiteSpace(check.SubmittedKeywords))
        {
            projectFullText += $" Tools: {check.SubmittedKeywords}";
        }
        string embeddingJson = _embeddingService.GetEmbeddingJson(projectFullText);

        var studentUser = await _context.Users.FindAsync(studentId);

        var newProject = new FypProject
        {
            Title = check.SubmittedTitle,
            Description = check.SubmittedText,
            Keywords = check.SubmittedKeywords,
            DepartmentId = dept?.Id ?? 1,
            OwnerId = studentId,
            StudentName = studentUser?.FullName ?? check.StudentName ?? User.Identity?.Name,
            UniversityName = studentUser?.University ?? check.UniversityName ?? "NUML Islamabad",
            Supervisor = string.IsNullOrWhiteSpace(supervisor) ? (check.SupervisorName ?? "To be assigned by Coordinator") : supervisor.Trim(),
            Year = DateTime.UtcNow.Year,
            Status = "Reserved",
            ReservationToken = token,
            EmbeddingJson = embeddingJson,
            CreatedAt = DateTime.UtcNow
        };

        _context.FypProjects.Add(newProject);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Idea reserved successfully! Your official Reservation Token is: {token}";
        return RedirectToAction("Report", new { id = checkId });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
