using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FypMatchTracker.Data;

namespace FypMatchTracker.Controllers;

/// <summary>
/// Public RESTful Web API Controller (Module 11).
/// Exposes read-only JSON endpoints for authorized FYPs and statistics.
/// Can be consumed by mobile apps or external NUML academic portals.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProjectsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProjectsApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET: /api/projectsapi/approved
    /// Returns list of approved and completed FYPs.
    /// </summary>
    [HttpGet("approved")]
    public async Task<IActionResult> GetApprovedProjects([FromQuery] int? year, [FromQuery] int? departmentId)
    {
        var query = _context.FypProjects
            .Include(p => p.Department)
            .Where(p => p.Status == "Approved" || p.Status == "Completed");

        if (year.HasValue)
        {
            query = query.Where(p => p.Year == year.Value);
        }

        if (departmentId.HasValue)
        {
            query = query.Where(p => p.DepartmentId == departmentId.Value);
        }

        var results = await query
            .OrderByDescending(p => p.Year)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Description,
                Student = p.StudentName ?? "Department Students",
                University = p.UniversityName ?? "NUML Islamabad",
                Department = p.Department != null ? p.Department.Name : "General",
                p.Year,
                p.Supervisor,
                Keywords = p.Keywords != null ? p.Keywords.Split(',', StringSplitOptions.TrimEntries) : new string[0],
                p.Status
            })
            .ToListAsync();

        return Ok(new
        {
            success = true,
            total = results.Count,
            data = results
        });
    }

    /// <summary>
    /// GET: /api/projectsapi/{id}
    /// Returns detailed metadata for a single project.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProjectById(int id)
    {
        var project = await _context.FypProjects
            .Include(p => p.Department)
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Description,
                Student = p.StudentName ?? "Department Students",
                University = p.UniversityName ?? "NUML Islamabad",
                Department = p.Department != null ? p.Department.Name : "General",
                p.Year,
                p.Supervisor,
                p.Keywords,
                p.Status,
                p.ReservationToken,
                p.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (project == null)
        {
            return NotFound(new { success = false, message = "Project not found." });
        }

        return Ok(new { success = true, data = project });
    }

    /// <summary>
    /// GET: /api/projectsapi/stats or /api/projectsapi/statistics
    /// Returns high-level repository statistics.
    /// </summary>
    [HttpGet("stats")]
    [HttpGet("statistics")]
    public async Task<IActionResult> GetStats()
    {
        int totalProjects = await _context.FypProjects.CountAsync();
        int totalChecks = await _context.SimilarityChecks.CountAsync();
        int departments = await _context.Departments.CountAsync();

        return Ok(new
        {
            success = true,
            totalArchiveProjects = totalProjects,
            totalSimilarityChecksRun = totalChecks,
            totalDepartments = departments,
            system = "NUML FYP Match Tracker v2.0"
        });
    }
}
