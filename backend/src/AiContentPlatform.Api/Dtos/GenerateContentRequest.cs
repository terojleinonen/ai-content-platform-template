namespace AiContentPlatform.Api.Dtos;

public class GenerateContentRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? TargetAudience { get; set; }
    public string? ToneOfVoice { get; set; }
    public string? Language { get; set; }
    public string[]? Keywords { get; set; }
}
