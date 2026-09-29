using AiContentPlatform.Api.Dtos;

namespace AiContentPlatform.Api.Services;

public interface IAiTextService
{
    Task<GenerateContentResponse> GenerateContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// A single text generation backend (mock, Anthropic, OpenAI, ...).
/// </summary>
public interface ITextGenerationProvider
{
    string Name { get; }

    Task<GeneratedText> GenerateAsync(GenerateContentRequest request, CancellationToken cancellationToken = default);
}

public record GeneratedText(string Title, string Body);
