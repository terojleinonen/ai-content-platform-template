using AiContentPlatform.Api.Dtos;

namespace AiContentPlatform.Api.Services;

public interface IAiTextService
{
    string ProviderName { get; }

    Task<GenerateContentResponse> GenerateContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Streams raw Markdown output ("# Title" first) as the provider produces it.</summary>
    IAsyncEnumerable<string> StreamContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Parses complete streamed output into a title/body response with SEO metrics.</summary>
    GenerateContentResponse BuildResponse(GenerateContentRequest request, string output);
}

/// <summary>
/// A single text generation backend (mock, Anthropic, OpenAI, ...).
/// </summary>
public interface ITextGenerationProvider
{
    string Name { get; }

    /// <summary>
    /// Streams Markdown text chunks. The first line of the full output is the title as a "# " heading.
    /// </summary>
    IAsyncEnumerable<string> StreamAsync(GenerateContentRequest request, CancellationToken cancellationToken = default);
}

public record GeneratedText(string Title, string Body);
