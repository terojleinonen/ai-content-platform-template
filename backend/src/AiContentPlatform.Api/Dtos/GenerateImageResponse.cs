namespace AiContentPlatform.Api.Dtos;

public class GenerateImageResponse
{
    /// <summary>An http(s) URL or a data: URI.</summary>
    public string Url { get; set; } = string.Empty;
    public string? PromptUsed { get; set; }
    public string Provider { get; set; } = string.Empty;
}
