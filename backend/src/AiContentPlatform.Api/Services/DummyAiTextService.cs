using AiContentPlatform.Api.Dtos;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Dummy AI service that returns hard-coded content.
/// Replace with calls to a real AI provider (OpenAI, Azure, etc.).
/// </summary>
public class DummyAiTextService : IAiTextService
{
    private readonly ISeoScoringService _seo;

    public DummyAiTextService(ISeoScoringService seo)
    {
        _seo = seo;
    }

    public Task<GenerateContentResponse> GenerateContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default)
    {
        var body = $"""This is placeholder AI-generated content based on your prompt:

"{request.Prompt}"

TODO: Wire this up to a real AI provider and remove this dummy implementation.
""".Trim();

        var keywords = request.Keywords ?? Array.Empty<string>();
        var scores = _seo.ScoreKeywords(body, keywords);
        var summary = _seo.BuildSeoSummary(body, keywords);

        var response = new GenerateContentResponse
        {
            Title = request.Title ?? "Placeholder Title",
            Body = body,
            SeoSummary = summary,
            KeywordScores = scores
        };

        return Task.FromResult(response);
    }
}
