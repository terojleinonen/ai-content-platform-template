namespace AiContentPlatform.Api.Dtos;

public class GenerateContentResponse
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? SeoSummary { get; set; }
    public Dictionary<string, double>? KeywordScores { get; set; }
}
