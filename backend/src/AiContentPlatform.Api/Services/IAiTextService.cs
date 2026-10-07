using System.Text;
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

    /// <summary>
    /// When the content is (or will be) in another language than the keywords and brand terms,
    /// translates them first and rewrites the request to use the translations, so the AI writes
    /// exactly the terms that are scored. Idempotent; the other methods call it themselves.
    /// </summary>
    Task LocalizeTermsAsync(GenerateContentRequest request, CancellationToken cancellationToken = default);

    /// <inheritdoc cref="LocalizeTermsAsync(GenerateContentRequest, CancellationToken)"/>
    Task LocalizeTermsAsync(TransformContentRequest request, CancellationToken cancellationToken = default);

    /// <summary>Turns complete output into the final response with SEO metrics and brand check.</summary>
    GenerateContentResponse BuildResponse(string output, GenerateContentRequest request);

    /// <inheritdoc cref="BuildResponse(string, GenerateContentRequest)"/>
    GenerateContentResponse BuildResponse(string output, TransformContentRequest request);
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

    /// <summary>Translates short terms (keywords, brand terms) into a language; same order and count.</summary>
    Task<IReadOnlyList<string>> TranslateTermsAsync(IReadOnlyList<string> terms, string language, CancellationToken cancellationToken = default);
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

    public async Task<IReadOnlyList<string>> TranslateTermsAsync(IReadOnlyList<string> terms, string language, CancellationToken cancellationToken = default)
    {
        var output = new StringBuilder();
        await foreach (var chunk in StreamCompletionAsync(
            ContentPrompt.TermTranslationSystemPrompt, ContentPrompt.BuildTermTranslationPrompt(terms, language), cancellationToken))
        {
            output.Append(chunk);
        }
        return ContentPrompt.ParseTermList(output.ToString(), terms);
    }

    protected abstract IAsyncEnumerable<string> StreamCompletionAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);
}

public record GeneratedText(string Title, string Body);
