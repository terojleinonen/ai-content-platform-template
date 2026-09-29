using System.Globalization;
using System.Text.RegularExpressions;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Simple keyword-density based SEO scoring. Replace with ML.NET or a dedicated SEO API
/// if you need readability, semantic coverage, SERP analysis, etc.
/// </summary>
public partial class SeoScoringService : ISeoScoringService
{
    // Healthy keyword density range; above the upper bound reads as keyword stuffing.
    public const double LowDensity = 0.005;
    public const double HighDensity = 0.03;

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();

    public Dictionary<string, double> ScoreKeywords(string content, IEnumerable<string> keywords)
    {
        var scores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var words = Tokenize(content);

        foreach (var keyword in keywords)
        {
            if (string.IsNullOrWhiteSpace(keyword) || scores.ContainsKey(keyword.Trim())) continue;

            var phrase = Tokenize(keyword);
            var occurrences = CountOccurrences(words, phrase);
            var density = words.Count == 0 ? 0.0 : (double)occurrences * phrase.Count / words.Count;
            scores[keyword.Trim()] = Math.Round(density, 4);
        }

        return scores;
    }

    public string BuildSeoSummary(string content, IEnumerable<string> keywords)
    {
        var wordCount = CountWords(content);
        var scores = ScoreKeywords(content, keywords);
        if (scores.Count == 0)
        {
            return $"{wordCount} words. No SEO keywords provided — add some to optimize your content.";
        }

        var parts = scores.Select(kvp =>
            $"{kvp.Key}: {(kvp.Value * 100).ToString("0.0", CultureInfo.InvariantCulture)}% ({Rate(kvp.Value)})");
        return $"{wordCount} words. Keyword density: " + string.Join(", ", parts) + ".";
    }

    public int CountWords(string content) => Tokenize(content).Count;

    public static string Rate(double density) => density switch
    {
        0 => "missing",
        < LowDensity => "low",
        <= HighDensity => "good",
        _ => "too high"
    };

    private static List<string> Tokenize(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? new List<string>()
            : WordRegex().Matches(text.ToLowerInvariant()).Select(m => m.Value).ToList();

    private static int CountOccurrences(List<string> words, List<string> phrase)
    {
        if (phrase.Count == 0 || phrase.Count > words.Count) return 0;

        var count = 0;
        for (var i = 0; i <= words.Count - phrase.Count; i++)
        {
            var match = true;
            for (var j = 0; j < phrase.Count && match; j++)
            {
                match = words[i + j] == phrase[j];
            }

            if (match) count++;
        }

        return count;
    }
}
