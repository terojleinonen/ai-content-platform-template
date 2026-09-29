using AiContentPlatform.Api.Services;

namespace AiContentPlatform.Api.Tests;

public class SeoScoringServiceTests
{
    private readonly SeoScoringService _seo = new();

    [Fact]
    public void ScoreKeywords_MatchesMultiWordPhrases()
    {
        var scores = _seo.ScoreKeywords("Content marketing works. Great content marketing wins.", ["content marketing"]);

        // 2 occurrences * 2 words / 7 words
        Assert.Equal(Math.Round(4.0 / 7, 4), scores["content marketing"]);
    }

    [Fact]
    public void ScoreKeywords_IsCaseInsensitiveAndIgnoresPunctuation()
    {
        var scores = _seo.ScoreKeywords("COFFEE! Coffee, coffee.", ["coffee"]);

        Assert.Equal(1.0, scores["coffee"]);
    }

    [Fact]
    public void ScoreKeywords_HandlesNonAsciiText()
    {
        var scores = _seo.ScoreKeywords("Hyvää kahvia ja hyvää seuraa", ["hyvää"]);

        Assert.Equal(0.4, scores["hyvää"]);
    }

    [Fact]
    public void ScoreKeywords_ReturnsZeroForMissingKeywordAndSkipsBlank()
    {
        var scores = _seo.ScoreKeywords("Some text here", ["absent", " ", ""]);

        Assert.Single(scores);
        Assert.Equal(0, scores["absent"]);
    }

    [Theory]
    [InlineData(0, "missing")]
    [InlineData(0.001, "low")]
    [InlineData(0.02, "good")]
    [InlineData(0.08, "too high")]
    public void Rate_ClassifiesDensity(double density, string expected) =>
        Assert.Equal(expected, SeoScoringService.Rate(density));

    [Fact]
    public void BuildSeoSummary_IncludesWordCountAndDensities()
    {
        var summary = _seo.BuildSeoSummary("one two three four coffee", ["coffee"]);

        Assert.Equal("5 words. Keyword density: coffee: 20.0% (too high).", summary);
    }
}
