namespace FypMatchTracker.Models;

/// <summary>
/// A single match result linking a check to either an internal FYP project or an external paper.
/// Matches ER diagram table: SimilarityMatch
/// </summary>
public class SimilarityMatch
{
    // Primary Key
    public int Id { get; set; }

    // Foreign Key to the parent check
    public int CheckId { get; set; }
    public SimilarityCheck? SimilarityCheck { get; set; }

    // Foreign Key to an internal FYP (optional)
    public int? FypProjectId { get; set; }
    public FypProject? FypProject { get; set; }

    // Foreign Key to an external research paper (optional)
    public int? ExternalPaperId { get; set; }
    public ExternalPaper? ExternalPaper { get; set; }

    // Semantic Meaning Similarity Score (0 to 100)
    public double SemanticScore { get; set; }

    // Keyword Jaccard Similarity Score (0 to 100)
    public double KeywordScore { get; set; }

    // Final Weighted Similarity Score (e.g. 0.7 * Semantic + 0.3 * Keyword)
    public double FinalScore { get; set; }
}
