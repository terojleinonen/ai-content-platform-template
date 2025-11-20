namespace AiContentPlatform.Api.Dtos;

public class GenerateImageRequest
{
    public string Prompt { get; set; } = string.Empty;
    public string? Style { get; set; }
    public int Width { get; set; } = 1024;
    public int Height { get; set; } = 1024;
}
