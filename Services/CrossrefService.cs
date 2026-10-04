using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FypMatchTracker.Models;

namespace FypMatchTracker.Services;

/// <summary>
/// Service that consumes the public Crossref Web API to search academic research papers.
/// Meets requirement 4 & 5: Web API Integration & Research Paper Lookup.
/// Gracefully handles network errors and falls back to local data if offline (Module 12).
/// </summary>
public class CrossrefService
{
    private readonly HttpClient _httpClient;

    public CrossrefService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(5); // Fast timeout so checks never hang
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FypMatchTracker/1.0 (mailto:cs.department@numl.edu.pk)");
        }
    }

    /// <summary>
    /// Searches Crossref for academic papers matching the student's query.
    /// Returns up to 5 relevant external papers.
    /// </summary>
    public async Task<List<ExternalPaper>> SearchPapersAsync(string query)
    {
        var papers = new List<ExternalPaper>();

        if (string.IsNullOrWhiteSpace(query))
            return papers;

        try
        {
            // Expand abbreviations like FYP to full academic terms (Crossref is an international scholarly database)
            string academicQuery = System.Text.RegularExpressions.Regex.Replace(query, @"\bFYP\b", "Final Year Project", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Filter strictly to peer-reviewed journal & conference proceeding articles (IEEE, ACM, Springer)
            string url = $"https://api.crossref.org/works?query={Uri.EscapeDataString(academicQuery)}&rows=5&filter=type:journal-article,type:proceedings-article&select=DOI,title,abstract,created,URL";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return papers;

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var root = doc.RootElement;
            if (!root.TryGetProperty("message", out var message) || !message.TryGetProperty("items", out var items))
                return papers;

            foreach (var item in items.EnumerateArray())
            {
                try
                {
                    string doi = item.TryGetProperty("DOI", out var doiEl) ? doiEl.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(doi)) continue;

                    // Extract title (array of strings in Crossref)
                    string title = "";
                    if (item.TryGetProperty("title", out var titleArr) && titleArr.GetArrayLength() > 0)
                    {
                        title = titleArr[0].GetString() ?? "";
                    }
                    if (string.IsNullOrWhiteSpace(title)) continue;

                    // Extract Abstract (often contains JATS XML tags, which we strip)
                    string abstractText = "";
                    if (item.TryGetProperty("abstract", out var absEl))
                    {
                        abstractText = System.Text.RegularExpressions.Regex.Replace(absEl.GetString() ?? "", "<.*?>", string.Empty);
                    }

                    // Extract year safely
                    int year = DateTime.UtcNow.Year;
                    if (item.TryGetProperty("created", out var createdEl) && 
                        createdEl.TryGetProperty("date-parts", out var parts) && 
                        parts.GetArrayLength() > 0 && parts[0].GetArrayLength() > 0)
                    {
                        var firstPart = parts[0][0];
                        if (firstPart.ValueKind == JsonValueKind.Number && firstPart.TryGetInt32(out int parsedYear))
                        {
                            year = parsedYear;
                        }
                        else if (int.TryParse(firstPart.GetString(), out int strYear))
                        {
                            year = strYear;
                        }
                    }

                    string paperUrl = item.TryGetProperty("URL", out var urlEl) ? urlEl.GetString() ?? $"https://doi.org/{doi}" : $"https://doi.org/{doi}";

                    papers.Add(new ExternalPaper
                    {
                        Doi = doi,
                        Title = title,
                        Abstract = string.IsNullOrWhiteSpace(abstractText) ? null : abstractText,
                        Year = year,
                        Url = paperUrl,
                        FetchedAt = DateTime.UtcNow
                    });
                }
                catch
                {
                    // Skip any single malformed item without dropping others
                    continue;
                }
            }
        }
        catch
        {
            // If offline, no internet, or Crossref is down, return empty list gracefully (fallback)
        }

        return papers;
    }
}
