using System.Collections.Generic;

namespace FypMatchTracker.Models;

/// <summary>
/// Academic Department (e.g. Computer Science, Software Engineering)
/// Matches ER diagram table: Department
/// </summary>
public class Department
{
    // Primary Key
    public int Id { get; set; }

    // Department Name (e.g. "Computer Science")
    public string Name { get; set; } = string.Empty;

    // Department Code (e.g. "CS", "SE", "AI", "IT")
    public string? Code { get; set; }

    // Navigation Property: All FYPs belonging to this department
    public ICollection<FypProject> FypProjects { get; set; } = new List<FypProject>();
}
