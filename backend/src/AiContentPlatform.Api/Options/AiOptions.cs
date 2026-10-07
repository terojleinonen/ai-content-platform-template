namespace AiContentPlatform.Api.Options;

public class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>"Auto", "Mock", "Anthropic" or "OpenAI".</summary>
    public string TextProvider { get; set; } = "Auto";

    /// <summary>"Auto", "Mock" or "OpenAI".</summary>
    public string ImageProvider { get; set; } = "Auto";

    public MockOptions Mock { get; set; } = new();
    public AnthropicOptions Anthropic { get; set; } = new();
    public OpenAiOptions OpenAI { get; set; } = new();
}

public class MockOptions
{
    /// <summary>Delay between streamed words, to simulate a model typing. 0 disables it.</summary>
    public int StreamDelayMs { get; set; } = 25;
}

public class AnthropicOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-sonnet-5-5";
    public int MaxTokens { get; set; } = 2048;
    public string BaseUrl { get; set; } = "https://api.anthropic.com/";
}

public class OpenAiOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-4.1-mini";
    public string ImageModel { get; set; } = "gpt-image-1";
    public string BaseUrl { get; set; } = "https://api.openai.com/";
}
