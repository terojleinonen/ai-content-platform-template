using System.Text;
using AiContentPlatform.Api.Dtos;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Orchestrates content generation and editing: delegates writing to the configured provider,
/// then enriches the result with SEO metrics.
/// </summary>
public class AiTextService : IAiTextService
{
    private readonly ITextGenerationProvider _provider;
    private readonly ISeoScoringService _seo;

    public AiTextService(ITextGenerationProvider provider, ISeoScoringService seo)
    {
        _provider = provider;
        _seo = seo;
    }

    public string ProviderName => _provider.Name;

    public async Task<GenerateContentResponse> GenerateContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default)
    {
        var output = await CollectAsync(_provider.StreamContentAsync(request, cancellationToken));
        return BuildResponse(output, request.Title, request.Keywords);
    }

    public IAsyncEnumerable<string> StreamContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default) =>
        _provider.StreamContentAsync(request, cancellationToken);

    public async Task<IReadOnlyList<GenerateContentResponse>> GenerateVariantsAsync(GenerateContentRequest request, int count, CancellationToken cancellationToken = default)
    {
        var tasks = Enumerable.Range(1, count)
            .Select(i => GenerateContentAsync(request.AsVariant(i), cancellationToken));
        return await Task.WhenAll(tasks);
    }

    public async Task<GenerateContentResponse> TransformContentAsync(TransformContentRequest request, CancellationToken cancellationToken = default)
    {
        var output = await CollectAsync(_provider.StreamTransformAsync(request, cancellationToken));
        return BuildResponse(output, request.Title, request.Keywords);
    }

    public IAsyncEnumerable<string> StreamTransformAsync(TransformContentRequest request, CancellationToken cancellationToken = default) =>
        _provider.StreamTransformAsync(request, cancellationToken);

    public GenerateContentResponse BuildResponse(string output, string? fallbackTitle, IEnumerable<string>? keywords)
    {
        var generated = ContentPrompt.Parse(output, fallbackTitle);
        var cleanKeywords = (keywords ?? [])
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .ToArray();

        return new GenerateContentResponse
        {
            Title = generated.Title,
            Body = generated.Body,
            SeoSummary = _seo.BuildSeoSummary(generated.Body, cleanKeywords),
            KeywordScores = _seo.ScoreKeywords(generated.Body, cleanKeywords),
            WordCount = _seo.CountWords(generated.Body),
            Provider = _provider.Name
        };
    }

    private async Task<string> CollectAsync(IAsyncEnumerable<string> chunks)
    {
        var output = new StringBuilder();
        await foreach (var chunk in chunks)
        {
            output.Append(chunk);
        }

        if (string.IsNullOrWhiteSpace(output.ToString()))
        {
            throw new AiProviderException(_provider.Name, "Empty response.");
        }

        return output.ToString();
    }
}
