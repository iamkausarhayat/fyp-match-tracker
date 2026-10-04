using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace FypMatchTracker.Services;

/// <summary>
/// Service responsible for keyword-based comparison using Jaccard Similarity.
/// Directly implements the Class Diagram specification from Page 6 of the assignment PDF.
/// Uses collections: HashSet<string> and Dictionary<string, int>.
/// </summary>
public class KeywordService
{
    // List of common English stop words to ignore during keyword comparison
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "about", "above", "after", "again", "against", "all", "am", "an", "and", "any", "are", 
        "as", "at", "be", "because", "been", "before", "being", "below", "between", "both", "but", 
        "by", "can", "could", "did", "do", "does", "doing", "down", "during", "each", "few", "for", 
        "from", "further", "had", "has", "have", "having", "he", "her", "here", "hers", "herself", 
        "him", "himself", "his", "how", "i", "if", "in", "into", "is", "it", "its", "itself", "just", 
        "me", "more", "most", "my", "myself", "no", "nor", "not", "now", "of", "off", "on", "once", 
        "only", "or", "other", "our", "ours", "ourselves", "out", "over", "own", "s", "same", "she", 
        "should", "so", "some", "such", "than", "that", "the", "their", "theirs", "them", "themselves", 
        "then", "there", "these", "they", "this", "those", "through", "to", "too", "under", "until", 
        "up", "very", "was", "we", "were", "what", "when", "where", "which", "while", "who", "whom", 
        "why", "with", "would", "you", "your", "yours", "yourself", "yourselves", "using", "system", 
        "project", "based", "approach", "via", "study", "analysis", "student", "students", "university",
        "department", "web", "application", "applications", "systems", "projects", "management", 
        "developed", "proposed", "implementation", "solution", "module", "architecture", "technique", 
        "paper", "data", "software", "method", "user", "users", "online", "real", "time"
    };

    /// <summary>
    /// Extracts meaningful keyword tokens from text, removing punctuation and stop-words.
    /// Uses HashSet<string> as required by assignment guidelines.
    /// </summary>
    public HashSet<string> ExtractKeywords(string text)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(text))
            return keywords;

        // Split text by non-alphanumeric characters
        var words = Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9#+]");

        foreach (var word in words)
        {
            var cleaned = word.Trim();
            // Keep words with at least 3 letters that are not stop words
            if (cleaned.Length >= 3 && !StopWords.Contains(cleaned))
            {
                keywords.Add(cleaned);
            }
        }

        return keywords;
    }

    /// <summary>
    /// Calculates the Jaccard Similarity Score between two texts:
    /// J(A, B) = |Intersection(A, B)| / |Union(A, B)|
    /// Returns a percentage between 0 and 100.
    /// </summary>
    public double JaccardScore(string textA, string textB)
    {
        var setA = ExtractKeywords(textA);
        var setB = ExtractKeywords(textB);

        if (setA.Count == 0 || setB.Count == 0)
            return 0.0;

        // Number of shared keywords
        int intersectionCount = setA.Count(word => setB.Contains(word));

        // Total unique keywords across both sets
        var unionSet = new HashSet<string>(setA, StringComparer.OrdinalIgnoreCase);
        unionSet.UnionWith(setB);

        if (unionSet.Count == 0)
            return 0.0;

        double similarityRatio = (double)intersectionCount / unionSet.Count;
        return Math.Round(similarityRatio * 100.0, 1);
    }

    /// <summary>
    /// Counts word frequencies in text.
    /// Uses Dictionary<string, int> as required by assignment guidelines.
    /// </summary>
    public Dictionary<string, int> WordCounts(string text)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(text))
            return counts;

        var words = Regex.Split(text.ToLowerInvariant(), @"[^a-z0-9#+]");

        foreach (var word in words)
        {
            var cleaned = word.Trim();
            if (cleaned.Length >= 3 && !StopWords.Contains(cleaned))
            {
                if (counts.ContainsKey(cleaned))
                    counts[cleaned]++;
                else
                    counts[cleaned] = 1;
            }
        }

        return counts;
    }

    /// <summary>
    /// Finds overlapping shared keywords between two texts for display in report suggestions.
    /// </summary>
    public List<string> GetSharedKeywords(string textA, string textB)
    {
        var setA = ExtractKeywords(textA);
        var setB = ExtractKeywords(textB);

        return setA.Where(word => setB.Contains(word)).ToList();
    }

    /// <summary>
    /// Extracts the top N most frequent keywords from a text.
    /// </summary>
    public List<string> ExtractTopKeywords(string text, int count = 5)
    {
        var counts = WordCounts(text);
        return counts.OrderByDescending(kv => kv.Value)
                     .Take(count)
                     .Select(kv => kv.Key)
                     .ToList();
    }
}
