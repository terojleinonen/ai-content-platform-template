namespace AiContentPlatform.Api.Dtos;

public class GenerateImageResponse
{
    public string Url { get; set; } = string.Empty;
    public string? PromptUsed { get; set; }
}
