using System.Text;
using AiContentPlatform.Api.Dtos;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Orchestrates content generation: delegates writing to the configured provider,
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
        var output = new StringBuilder();
        await foreach (var chunk in _provider.StreamAsync(request, cancellationToken))
        {
            output.Append(chunk);
        }

        if (string.IsNullOrWhiteSpace(output.ToString()))
        {
            throw new AiProviderException(_provider.Name, "Empty response.");
        }

        return BuildResponse(request, output.ToString());
    }

    public IAsyncEnumerable<string> StreamContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default) =>
        _provider.StreamAsync(request, cancellationToken);

    public GenerateContentResponse BuildResponse(GenerateContentRequest request, string output)
    {
        var generated = ContentPrompt.Parse(output, request);
        var keywords = (request.Keywords ?? Array.Empty<string>())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .ToArray();

        return new GenerateContentResponse
        {
            Title = generated.Title,
            Body = generated.Body,
            SeoSummary = _seo.BuildSeoSummary(generated.Body, keywords),
            KeywordScores = _seo.ScoreKeywords(generated.Body, keywords),
            WordCount = _seo.CountWords(generated.Body),
            Provider = _provider.Name
        };
    }
}
