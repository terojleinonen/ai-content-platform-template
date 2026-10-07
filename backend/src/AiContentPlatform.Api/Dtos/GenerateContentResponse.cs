namespace AiContentPlatform.Api.Dtos;

public class GenerateContentResponse
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? SeoSummary { get; set; }
    public Dictionary<string, double>? KeywordScores { get; set; }
    public int WordCount { get; set; }
    public string Provider { get; set; } = string.Empty;

    /// <summary>Present when the content was written for a project with a brand voice.</summary>
    public BrandCheckResult? BrandCheck { get; set; }
}
