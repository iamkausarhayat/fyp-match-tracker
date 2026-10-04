using System;
using System.Collections.Generic;

namespace FypMatchTracker.Models;

/// <summary>
/// A Final Year Project record in the repository.
/// Matches ER diagram table: FypProject
/// </summary>
public class FypProject
{
    // Primary Key
    public int Id { get; set; }

    // Project Title (e.g. "Smart Attendance System using Face Recognition")
    public string Title { get; set; } = string.Empty;

    // Detailed Abstract or description (Optional if supervisor provided only title)
    public string? Description { get; set; }

    // Tools and key phrases (e.g. "Python, OpenCV, FaceNet")
    public string? Keywords { get; set; }

    // Academic Year (e.g. 2023, 2024, 2026)
    public int Year { get; set; } = DateTime.UtcNow.Year;

    // Status: "Completed", "Approved", "Reserved"
    public string Status { get; set; } = "Completed";

    // Optional Faculty Supervisor
    public string? Supervisor { get; set; }

    // Student Name(s) who worked on or proposed this project (e.g. "Kausar Khattak, Mudassir Ahmad")
    public string? StudentName { get; set; }

    // University / Institution (e.g. "NUML Islamabad")
    public string UniversityName { get; set; } = "NUML Islamabad";

    // Optional Reservation Token if student reserved it (e.g. "NUML-FYP-2026-9842")
    public string? ReservationToken { get; set; }

    // Serialized 384-dimension vector embeddings from Local ONNX model
    public string? EmbeddingJson { get; set; }

    // Foreign Key to Department
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }

    // Foreign Key to Student (optional: null for past department records)
    public int? OwnerId { get; set; }
    public AppUser? Owner { get; set; }

    // Timestamp
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Property: All similarity checks that matched this project
    public ICollection<SimilarityMatch> SimilarityMatches { get; set; } = new List<SimilarityMatch>();
}
