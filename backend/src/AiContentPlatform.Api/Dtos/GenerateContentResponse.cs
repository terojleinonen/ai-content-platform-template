namespace AiContentPlatform.Api.Dtos;

public class GenerateContentResponse
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? SeoSummary { get; set; }
    public Dictionary<string, double>? KeywordScores { get; set; }

    /// <summary>Keyword usage rated for the content's type and length (mentions or density).</summary>
    public SeoReport? Seo { get; set; }
    public int WordCount { get; set; }
    public string Provider { get; set; } = string.Empty;

    /// <summary>Present when the content was written for a project with a brand voice.</summary>
    public BrandCheckResult? BrandCheck { get; set; }

    /// <summary>
    /// Keywords and brand terms translated into the content's language (original → translated),
    /// when it differs from the language they were given in. Scores above use the translations.
    /// </summary>
    public Dictionary<string, string>? TermTranslations { get; set; }

    /// <summary>Tokens and estimated cost of the main AI call (term translation is recorded separately).</summary>
    public AiUsageSummary? Usage { get; set; }
}

public record AiUsageSummary(string Model, int InputTokens, int OutputTokens, double? CostUsd, bool Estimated);

