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

    public async Task<GenerateContentResponse> GenerateContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default)
    {
        var generated = await _provider.GenerateAsync(request, cancellationToken);

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
