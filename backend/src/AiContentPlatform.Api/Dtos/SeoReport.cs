using System.Globalization;
using System.Text.Json.Serialization;
using AiContentPlatform.Api.Domain;

namespace AiContentPlatform.Api.Dtos;

public enum SeoMode
{
    /// <summary>Keyword density (share of words) against a target range; for longer text.</summary>
    Density,

    /// <summary>Number of mentions; density is meaningless for very short text.</summary>
    Mentions
}

/// <summary>How keyword usage is judged for a piece of content, based on its type and length.</summary>
public record SeoRules(SeoMode Mode, double LowDensity, double HighDensity, int MaxMentions, string Target)
{
    public const int ShortTextWords = 80;
    public const int MediumTextWords = 250;

    public static readonly SeoRules Short = new(SeoMode.Mentions, 0, 0, 2, "Short text: mention each keyword once or twice.");
    public static readonly SeoRules Medium = new(SeoMode.Density, 0.005, 0.05, 0, "Target density: 0.5%–5% per keyword.");
    public static readonly SeoRules Long = new(SeoMode.Density, 0.005, 0.03, 0, "Target density: 0.5%–3% per keyword.");

    public static SeoRules For(ContentType? type, int wordCount) =>
        type == ContentType.SocialPost || wordCount < ShortTextWords ? Short
        : type == ContentType.ProductDescription || wordCount < MediumTextWords ? Medium
        : Long;

    public string Rate(int occurrences, double density) =>
        occurrences == 0 ? "missing"
        : Mode == SeoMode.Mentions ? (occurrences <= MaxMentions ? "good" : "too many")
        : density < LowDensity ? "low"
        : density <= HighDensity ? "good"
        : "too high";
}

public record KeywordInsight(string Keyword, int Occurrences, double Density, string Rating);

public record SeoReport(SeoMode Mode, string Target, int WordCount, IReadOnlyList<KeywordInsight> Keywords)
{
    /// <summary>One-line text version; exposed separately as <c>seoSummary</c> on responses.</summary>
    [JsonIgnore]
    public string Summary
    {
        get
        {
            if (Keywords.Count == 0)
            {
                return $"{WordCount} words. No SEO keywords provided — add some to optimize your content.";
            }

            var parts = Keywords.Select(k => Mode == SeoMode.Mentions
                ? $"{k.Keyword}: {k.Occurrences}× ({k.Rating})"
                : $"{k.Keyword}: {(k.Density * 100).ToString("0.0", CultureInfo.InvariantCulture)}% ({k.Rating})");
            var label = Mode == SeoMode.Mentions ? "Keyword mentions" : "Keyword density";
            return $"{WordCount} words. {label}: {string.Join(", ", parts)}.";
        }
    }
}
