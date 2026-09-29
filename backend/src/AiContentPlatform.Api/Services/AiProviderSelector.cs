using AiContentPlatform.Api.Options;

namespace AiContentPlatform.Api.Services;

public static class AiProviderNames
{
    public const string Mock = "Mock";
    public const string Anthropic = "Anthropic";
    public const string OpenAI = "OpenAI";
}

/// <summary>
/// Resolves the configured provider names, including the "Auto" mode which picks the first
/// provider that has an API key and falls back to the offline mock.
/// </summary>
public static class AiProviderSelector
{
    public static string ResolveText(AiOptions options) => Normalize(options.TextProvider) switch
    {
        "auto" when HasKey(options.Anthropic.ApiKey) => AiProviderNames.Anthropic,
        "auto" when HasKey(options.OpenAI.ApiKey) => AiProviderNames.OpenAI,
        "auto" or "mock" => AiProviderNames.Mock,
        "anthropic" => Require(AiProviderNames.Anthropic, options.Anthropic.ApiKey),
        "openai" => Require(AiProviderNames.OpenAI, options.OpenAI.ApiKey),
        _ => throw new InvalidOperationException($"Unknown Ai:TextProvider '{options.TextProvider}'.")
    };

    public static string ResolveImage(AiOptions options) => Normalize(options.ImageProvider) switch
    {
        "auto" when HasKey(options.OpenAI.ApiKey) => AiProviderNames.OpenAI,
        "auto" or "mock" => AiProviderNames.Mock,
        "openai" => Require(AiProviderNames.OpenAI, options.OpenAI.ApiKey),
        _ => throw new InvalidOperationException($"Unknown Ai:ImageProvider '{options.ImageProvider}'.")
    };

    private static string Normalize(string? value) => (value ?? "auto").Trim().ToLowerInvariant();

    private static bool HasKey(string? key) => !string.IsNullOrWhiteSpace(key);

    private static string Require(string provider, string? key) => HasKey(key)
        ? provider
        : throw new InvalidOperationException($"{provider} is selected but no API key is configured (Ai:{provider}:ApiKey).");
}
