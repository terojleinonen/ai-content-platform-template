using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;
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
    [InlineData(ContentType.SocialPost, 500, SeoMode.Mentions)]
    [InlineData(ContentType.BlogPost, 50, SeoMode.Mentions)]        // any type, if very short
    [InlineData(ContentType.ProductDescription, 600, SeoMode.Density)]
    [InlineData(null, 30, SeoMode.Mentions)]
    public void Rules_PickModeByTypeAndLength(ContentType? type, int words, SeoMode expected) =>
        Assert.Equal(expected, SeoRules.For(type, words).Mode);

    [Theory]
    [InlineData(ContentType.BlogPost, 500, 0.04, "too high")]       // long text: 0.5–3%
    [InlineData(ContentType.ProductDescription, 500, 0.04, "good")] // product text: 0.5–5%
    [InlineData(ContentType.BlogPost, 150, 0.04, "good")]           // medium length: 0.5–5%
    [InlineData(ContentType.BlogPost, 500, 0.002, "low")]
    public void Rules_RateDensityByTypeAndLength(ContentType type, int words, double density, string expected) =>
        Assert.Equal(expected, SeoRules.For(type, words).Rate(occurrences: 3, density));

    [Theory]
    [InlineData(0, "missing")]
    [InlineData(1, "good")]
    [InlineData(2, "good")]
    [InlineData(3, "too many")]
    public void Rules_RateMentionsForShortText(int occurrences, string expected) =>
        Assert.Equal(expected, SeoRules.Short.Rate(occurrences, density: 0.5));

    [Fact]
    public void Analyze_ShortSocialPost_RatesByMentions()
    {
        var report = _seo.Analyze("Pieni kahvila, iso näkyvyys. Sosiaalisen median markkinointi on arkea.",
            ["kahvila", "sosiaalisen median markkinointi"], ContentType.SocialPost);

        Assert.Equal(SeoMode.Mentions, report.Mode);
        Assert.All(report.Keywords, k => Assert.Equal("good", k.Rating));
        Assert.Equal("9 words. Keyword mentions: kahvila: 1× (good), sosiaalisen median markkinointi: 1× (good).", report.Summary);
    }

    [Fact]
    public void Analyze_LongText_RatesByDensity()
    {
        var text = string.Join(' ', Enumerable.Repeat("coffee beans roasted fresh every morning", 60));

        var report = _seo.Analyze(text, ["coffee"], ContentType.BlogPost);

        Assert.Equal(SeoMode.Density, report.Mode);
        Assert.Equal("too high", report.Keywords.Single().Rating); // 1 in 6 words
        Assert.StartsWith("360 words. Keyword density: coffee: 16.7% (too high)", report.Summary);
    }

    [Fact]
    public void ScoreKeywords_CountsFinnishInflectedForms()
    {
        const string text = "Pienyrittäjän arki on kiireinen. Pienyrittäjälle blogi on tärkeä, ja moni pienyrittäjä kirjoittaa blogia.";

        var scores = _seo.ScoreKeywords(text, ["pienyrittäjä", "blogi"]);

        // 3 forms of pienyrittäjä and 2 of blogi in 13 words.
        Assert.Equal(Math.Round(3.0 / 13, 4), scores["pienyrittäjä"]);
        Assert.Equal(Math.Round(2.0 / 13, 4), scores["blogi"]);
    }

    [Fact]
    public void ScoreKeywords_CountsEnglishPlurals()
    {
        var scores = _seo.ScoreKeywords("One bag today, two bags tomorrow and more bags later.", ["bag"]);

        Assert.Equal(Math.Round(3.0 / 10, 4), scores["bag"]);
    }
}
