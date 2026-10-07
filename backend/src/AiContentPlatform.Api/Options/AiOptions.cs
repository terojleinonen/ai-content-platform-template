namespace AiContentPlatform.Api.Options;

public class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>"Auto", "Mock", "Anthropic" or "OpenAI".</summary>
    public string TextProvider { get; set; } = "Auto";

    /// <summary>"Auto", "Mock" or "OpenAI".</summary>
    public string ImageProvider { get; set; } = "Auto";

    public MockOptions Mock { get; set; } = new();

    /// <summary>Price per model ID, used to estimate the cost of each call.</summary>
    public Dictionary<string, ModelPrice> Pricing { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public AnthropicOptions Anthropic { get; set; } = new();
    public OpenAiOptions OpenAI { get; set; } = new();
}

public class ModelPrice
{
    /// <summary>USD per million input tokens.</summary>
    public double InputPerMTok { get; set; }

    /// <summary>USD per million output tokens.</summary>
    public double OutputPerMTok { get; set; }
}

public class MockOptions
{
    /// <summary>Delay between streamed words, to simulate a model typing. 0 disables it.</summary>
    public int StreamDelayMs { get; set; } = 25;
}

public class AnthropicOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "claude-opus-5-5";

    /// <summary>
    /// Upper bound for thinking plus the written text (current models think before writing).
    /// Only tokens actually generated are billed, so this is a safety cap, not a target.
    /// </summary>
    public int MaxTokens { get; set; } = 16000;

    /// <summary>
    /// How much Claude thinks: "low", "medium", "high" or "max". Set explicitly because
    /// model defaults differ (Opus 5.5 defaults to medium, Sonnet 5.5 to high).
    /// </summary>
    public string Effort { get; set; } = "medium";
    public string BaseUrl { get; set; } = "https://api.anthropic.com/";

    /// <summary>
    /// Re-serve safety-classifier declines on Anthropic's recommended fallback model
    /// (<c>fallbacks: "default"</c>). Claude API only; sent for models that support it.
    /// </summary>
    public bool ServerSideFallback { get; set; } = true;
}

public class OpenAiOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-4.1-mini";
    public string ImageModel { get; set; } = "gpt-image-1";
    public string BaseUrl { get; set; } = "https://api.openai.com/";
}
