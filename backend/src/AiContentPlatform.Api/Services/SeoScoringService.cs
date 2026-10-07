using System.Text.RegularExpressions;
using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Keyword-usage SEO scoring, judged by content type and length (see <see cref="SeoRules"/>).
/// Replace with ML.NET or a dedicated SEO API if you need readability, semantic coverage, SERP analysis, etc.
/// </summary>
public partial class SeoScoringService : ISeoScoringService
{
    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();

    public SeoReport Analyze(string content, IEnumerable<string> keywords, ContentType? type = null, TextLanguage language = TextLanguage.Auto)
    {
        var words = Tokenize(content);
        if (language == TextLanguage.Auto) language = Languages.Detect(content);
        var rules = SeoRules.For(type, words.Count);

        var insights = new List<KeywordInsight>();
        foreach (var keyword in keywords.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()))
        {
            if (insights.Any(i => string.Equals(i.Keyword, keyword, StringComparison.OrdinalIgnoreCase))) continue;

            var phrase = Tokenize(keyword);
            var occurrences = CountOccurrences(words, phrase.Select(w => WordMatcher.For(w, language)).ToList());
            var density = words.Count == 0 ? 0.0 : Math.Round((double)occurrences * phrase.Count / words.Count, 4);
            insights.Add(new KeywordInsight(keyword, occurrences, density, rules.Rate(occurrences, density)));
        }

        return new SeoReport(rules.Mode, rules.Target, words.Count, insights);
    }

    public Dictionary<string, double> ScoreKeywords(string content, IEnumerable<string> keywords, TextLanguage language = TextLanguage.Auto) =>
        Analyze(content, keywords, language: language).Keywords
            .ToDictionary(k => k.Keyword, k => k.Density, StringComparer.OrdinalIgnoreCase);

    public int CountWords(string content) => Tokenize(content).Count;

    private static List<string> Tokenize(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? new List<string>()
            : WordRegex().Matches(text.ToLowerInvariant()).Select(m => m.Value).ToList();

    private static int CountOccurrences(List<string> words, List<Func<string, bool>> phrase)
    {
        if (phrase.Count == 0 || phrase.Count > words.Count) return 0;

        var count = 0;
        for (var i = 0; i <= words.Count - phrase.Count; i++)
        {
            var match = true;
            for (var j = 0; j < phrase.Count && match; j++)
            {
                match = phrase[j](words[i + j]);
            }

            if (match) count++;
        }

        return count;
    }
}
