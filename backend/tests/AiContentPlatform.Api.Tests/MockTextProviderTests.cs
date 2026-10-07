using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Services;

namespace AiContentPlatform.Api.Tests;

public class MockTextProviderTests
{
    private const string Body = """
        Hey there! 👋 This is a very good intro. It has a second sentence.

        ## Section
        Another paragraph here. With more detail.

        - one
        - two
        - three
        """;

    private static GeneratedText Transform(TransformAction action, Action<TransformContentRequest>? configure = null)
    {
        var request = new TransformContentRequest { Action = action, Title = "Title", Body = Body };
        configure?.Invoke(request);
        return MockTextProvider.Transform(request);
    }

    [Fact]
    public void Shorten_KeepsFirstSentencesAndTwoListItems()
    {
        var result = Transform(TransformAction.Shorten);

        Assert.DoesNotContain("second sentence", result.Body);
        Assert.Contains("## Section\nAnother paragraph here.", result.Body);
        Assert.DoesNotContain("With more detail", result.Body);
        Assert.Contains("- two", result.Body);
        Assert.DoesNotContain("- three", result.Body);
        Assert.True(result.Body.Length < Body.Length);
    }

    [Fact]
    public void Expand_MakesTextLonger()
    {
        Assert.True(Transform(TransformAction.Expand).Body.Length > Body.Length);
    }

    [Fact]
    public void Improve_ReplacesWeakPhrases()
    {
        var body = Transform(TransformAction.Improve).Body;

        Assert.Contains("excellent", body);
        Assert.DoesNotContain("very good", body);
    }

    [Fact]
    public void ChangeTone_SwapsTheOpener()
    {
        var result = Transform(TransformAction.ChangeTone, r => r.ToneOfVoice = "Professional");

        Assert.StartsWith("We are pleased to share an update. This is", result.Body);
        Assert.DoesNotContain("Hey there", result.Body);
    }

    [Fact]
    public void Translate_IsClearlyMarkedAsUnsupported()
    {
        var result = Transform(TransformAction.Translate, r => r.Language = "Finnish");

        Assert.Equal("[Finnish] Title", result.Title);
        Assert.Contains("can't translate", result.Body);
    }

    [Fact]
    public void Variants_GetDifferentTitles()
    {
        var request = new GenerateContentRequest { Prompt = "coffee tips", Type = ContentType.BlogPost };

        var titles = Enumerable.Range(1, 3).Select(i => MockTextProvider.Generate(request.AsVariant(i)).Title).ToList();

        Assert.Equal(3, titles.Distinct().Count());
    }
}
