using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FypMatchTracker.Data;

namespace FypMatchTracker.Controllers;

/// <summary>
/// Controller for student-specific pages (Check History, Reserved Tokens).
/// </summary>
[Authorize(Roles = "Student")]
public class StudentController : Controller
{
    private readonly ApplicationDbContext _context;

    public StudentController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ==========================================
    // GET: /Student/History
    // ==========================================
    [HttpGet]
    public async Task<IActionResult> History(string? query, string? verdict)
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(idClaim, out int studentId))
        {
            return RedirectToAction("Login", "Account");
        }

        var checksQuery = _context.SimilarityChecks
            .Where(c => c.StudentId == studentId);

        if (!string.IsNullOrWhiteSpace(query))
        {
            string q = query.Trim().ToLower();
            checksQuery = checksQuery.Where(c => c.SubmittedTitle.ToLower().Contains(q) || (c.SubmittedKeywords != null && c.SubmittedKeywords.ToLower().Contains(q)));
        }

        if (!string.IsNullOrWhiteSpace(verdict))
        {
            checksQuery = checksQuery.Where(c => c.Verdict == verdict);
        }

        var checks = await checksQuery
            .OrderByDescending(c => c.CheckedAt)
            .ToListAsync();

        ViewBag.Query = query;
        ViewBag.Verdict = verdict;

        // Fetch student's reserved projects
        var reservedProjects = await _context.FypProjects
            .Where(p => p.OwnerId == studentId)
            .Include(p => p.Department)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        ViewBag.ReservedProjects = reservedProjects;

        return View(checks);
    }
}
