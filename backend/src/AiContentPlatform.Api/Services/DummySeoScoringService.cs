using System.Text.RegularExpressions;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Very naive SEO scoring service – replace with ML.NET or more advanced logic.
/// </summary>
public class DummySeoScoringService : ISeoScoringService
{
    public Dictionary<string, double> ScoreKeywords(string content, IEnumerable<string> keywords)
    {
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(content))
        {
            return scores;
        }

        var words = Regex.Split(content.ToLowerInvariant(), "[^a-z0-9]+")
            .Where(w => !string.IsNullOrWhiteSpace(w))
            .ToList();

        var total = words.Count;

        foreach (var keyword in keywords)
        {
            if (string.IsNullOrWhiteSpace(keyword)) continue;

            var k = keyword.ToLowerInvariant();
            var count = words.Count(w => w.Contains(k));
            var score = total == 0 ? 0.0 : (double)count / total;
            scores[keyword] = Math.Round(score, 4);
        }

        return scores;
    }

    public string BuildSeoSummary(string content, IEnumerable<string> keywords)
    {
        var list = ScoreKeywords(content, keywords);
        if (!list.Any())
        {
            return "No SEO keywords provided yet. Add some to optimize your content.";
        }

        var parts = list.Select(kvp => $"{kvp.Key}: {kvp.Value:P1}");
        return "Keyword density (approximate): " + string.Join(", ", parts);
    }
}
