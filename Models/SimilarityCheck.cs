using System;
using System.Collections.Generic;

namespace FypMatchTracker.Models;

/// <summary>
/// A similarity verification check submitted by a student or guest.
/// Matches ER diagram table: SimilarityCheck
/// </summary>
public class SimilarityCheck
{
    // Primary Key
    public int Id { get; set; }

    // Submitted Idea Title
    public string SubmittedTitle { get; set; } = string.Empty;

    // Submitted Idea Description / Abstract
    public string SubmittedText { get; set; } = string.Empty;

    // Optional Tools / Keywords entered
    public string? SubmittedKeywords { get; set; }

    // Department selected (e.g. "Computer Science")
    public string? DepartmentName { get; set; }

    // Student's Full Name (e.g. "Kausar Khattak")
    public string? StudentName { get; set; }

    // Student's University Name (e.g. "NUML Islamabad")
    public string? UniversityName { get; set; }

    // Optional Proposed Supervisor Name
    public string? SupervisorName { get; set; }

    // Timestamp when checked
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;

    // Highest similarity score percentage (0 to 100)
    public double TopScore { get; set; }

    // Verdict: "UNIQUE", "SIMILAR", or "ALREADY DONE"
    public string Verdict { get; set; } = "UNIQUE";

    // Foreign Key to Student (optional: allows guest checks without login!)
    public int? StudentId { get; set; }
    public AppUser? Student { get; set; }

    // Navigation Property: Top matches found for this check
    public ICollection<SimilarityMatch> Matches { get; set; } = new List<SimilarityMatch>();
}
