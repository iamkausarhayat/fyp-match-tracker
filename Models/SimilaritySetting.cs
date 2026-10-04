using System;

namespace FypMatchTracker.Models;

/// <summary>
/// System configuration for thresholds and weights.
/// Matches ER diagram table: SimilaritySetting
/// </summary>
public class SimilaritySetting
{
    // Primary Key
    public int Id { get; set; }

    // Low Threshold cutoff for UNIQUE verdict (default 20.0%)
    public double LowThreshold { get; set; } = 20.0;

    // High Threshold cutoff for ALREADY DONE verdict (default 50.0%)
    public double HighThreshold { get; set; } = 50.0;

    // Weight of Semantic Meaning in final score (default 0.70)
    public double SemanticWeight { get; set; } = 0.70;

    // Weight of Keyword Match in final score (default 0.30)
    public double KeywordWeight { get; set; } = 0.30;

    // Toggle for querying external Crossref Web API (default true)
    public bool CrossrefApiEnabled { get; set; } = true;

    // Last updated timestamp
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
