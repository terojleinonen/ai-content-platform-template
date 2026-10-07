using AiContentPlatform.Api.Dtos;

namespace AiContentPlatform.Api.Services;

public interface IAiTextService
{
    string ProviderName { get; }

    Task<GenerateContentResponse> GenerateContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Streams raw Markdown output ("# Title" first) as the provider produces it.</summary>
    IAsyncEnumerable<string> StreamContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Generates several alternative versions of the same brief in parallel.</summary>
    Task<IReadOnlyList<GenerateContentResponse>> GenerateVariantsAsync(GenerateContentRequest request, int count, CancellationToken cancellationToken = default);

    Task<GenerateContentResponse> TransformContentAsync(TransformContentRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamTransformAsync(TransformContentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Parses complete output into a title/body response with SEO metrics.</summary>
    GenerateContentResponse BuildResponse(string output, string? fallbackTitle, IEnumerable<string>? keywords, BrandContext? brand = null);
}

/// <summary>
/// A single text generation backend (mock, Anthropic, OpenAI, ...).
/// Both methods stream Markdown chunks; the first line of the full output is the title as a "# " heading.
/// </summary>
public interface ITextGenerationProvider
{
    string Name { get; }

    IAsyncEnumerable<string> StreamContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamTransformAsync(TransformContentRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Base for LLM-backed providers: turns requests into prompts, subclasses only stream a completion.
/// </summary>
public abstract class LlmTextProvider : ITextGenerationProvider
{
    public abstract string Name { get; }

    public IAsyncEnumerable<string> StreamContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default) =>
        StreamCompletionAsync(ContentPrompt.SystemPrompt, ContentPrompt.BuildUserPrompt(request), cancellationToken);

    public IAsyncEnumerable<string> StreamTransformAsync(TransformContentRequest request, CancellationToken cancellationToken = default) =>
        StreamCompletionAsync(ContentPrompt.EditSystemPrompt, ContentPrompt.BuildTransformPrompt(request), cancellationToken);

    protected abstract IAsyncEnumerable<string> StreamCompletionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}

public record GeneratedText(string Title, string Body);
