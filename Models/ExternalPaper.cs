using System;
using System.Collections.Generic;

namespace FypMatchTracker.Models;

/// <summary>
/// A published academic research paper fetched from the Crossref Web API.
/// Matches ER diagram table: ExternalPaper
/// </summary>
public class ExternalPaper
{
    // Primary Key
    public int Id { get; set; }

    // Digital Object Identifier (Unique: e.g. "10.1109/TCE.2024.3382910")
    public string Doi { get; set; } = string.Empty;

    // Paper Title
    public string Title { get; set; } = string.Empty;

    // Abstract or summary
    public string? Abstract { get; set; }

    // Year published
    public int Year { get; set; }

    // Direct Web URL to the paper on Crossref / Publisher
    public string? Url { get; set; }

    // When this paper was fetched and cached in our database
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;

    // Serialized embedding vector
    public string? EmbeddingJson { get; set; }

    // Navigation Property: All matches pointing to this paper
    public ICollection<SimilarityMatch> SimilarityMatches { get; set; } = new List<SimilarityMatch>();
}
