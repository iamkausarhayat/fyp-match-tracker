using System;
using System.Collections.Generic;

namespace FypMatchTracker.Models;

/// <summary>
/// Represents a user in the system (Student or Coordinator).
/// Matches ER diagram table: AppUser
/// </summary>
public class AppUser
{
    // Primary Key
    public int Id { get; set; }

    // User's Full Name (e.g. Kausar Khattak)
    public string FullName { get; set; } = string.Empty;

    // Unique Institutional Email (e.g. s2258077@numl.edu.pk)
    public string Email { get; set; } = string.Empty;

    // Securely Hashed Password (using Microsoft.AspNetCore.Identity.PasswordHasher)
    public string PasswordHash { get; set; } = string.Empty;

    // Role: "Student" or "Coordinator"
    public string Role { get; set; } = "Student";

    // Status: "Active" or "Blocked"
    public string Status { get; set; } = "Active";

    // Optional Roll Number for students
    public string? RollNumber { get; set; }

    // University / Institution Name (e.g. NUML Islamabad)
    public string University { get; set; } = "NUML Islamabad";

    // Password reset token and expiry
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }

    // Account creation timestamp
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Optional Department association
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    // Navigation Properties
    public ICollection<SimilarityCheck> SimilarityChecks { get; set; } = new List<SimilarityCheck>();
    public ICollection<FypProject> FypProjects { get; set; } = new List<FypProject>();
}
