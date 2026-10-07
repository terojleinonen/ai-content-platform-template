using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Services;

namespace AiContentPlatform.Api.Tests;

public class ContentPromptTests
{
    [Fact]
    public void Parse_SplitsHeadingIntoTitle()
    {
        var result = ContentPrompt.Parse("# My Title\n\nFirst paragraph.\n\n## Section", null);

        Assert.Equal("My Title", result.Title);
        Assert.Equal("First paragraph.\n\n## Section", result.Body);
    }

    [Fact]
    public void Parse_StripsCodeFences()
    {
        var result = ContentPrompt.Parse("```markdown\n# Fenced\nBody\n```", null);

        Assert.Equal("Fenced", result.Title);
        Assert.Equal("Body", result.Body);
    }

    [Fact]
    public void Parse_FallsBackToRequestedTitle()
    {
        var result = ContentPrompt.Parse("Just a body.", "Requested");

        Assert.Equal("Requested", result.Title);
        Assert.Equal("Just a body.", result.Body);
    }

    [Fact]
    public void BuildUserPrompt_IncludesOnlyProvidedFields()
    {
        var prompt = ContentPrompt.BuildUserPrompt(new GenerateContentRequest
        {
            Prompt = "Launch our app",
            Type = ContentType.Email,
            ToneOfVoice = "Playful",
            Keywords = ["app", " ", "launch"]
        });

        Assert.Contains("Content type: Marketing email", prompt);
        Assert.Contains("Brief: Launch our app", prompt);
        Assert.Contains("Tone of voice: Playful", prompt);
        Assert.Contains("SEO keywords: app, launch", prompt);
        Assert.DoesNotContain("Target audience", prompt);
    }

    [Fact]
    public void BuildUserPrompt_AddsAngleOnlyForVariants()
    {
        var request = new GenerateContentRequest { Prompt = "x" };

        Assert.DoesNotContain("Angle:", ContentPrompt.BuildUserPrompt(request));
        Assert.NotEqual(
            ContentPrompt.BuildUserPrompt(request.AsVariant(1)),
            ContentPrompt.BuildUserPrompt(request.AsVariant(2)));
    }

    [Theory]
    [InlineData(TransformAction.Shorten, "50% shorter")]
    [InlineData(TransformAction.Translate, "into Finnish")]
    [InlineData(TransformAction.ChangeTone, "Playful tone")]
    [InlineData(TransformAction.Custom, "Add a call to action")]
    public void BuildTransformPrompt_DescribesTaskAndWrapsContent(TransformAction action, string expected)
    {
        var prompt = ContentPrompt.BuildTransformPrompt(new TransformContentRequest
        {
            Action = action,
            Title = "My title",
            Body = "Original body.",
            Language = "Finnish",
            ToneOfVoice = "Playful",
            Instruction = "Add a call to action",
            Keywords = ["seo"]
        });

        Assert.Contains(expected, prompt);
        Assert.Contains("SEO keywords to keep: seo", prompt);
        Assert.Contains("<content>\n# My title\n\nOriginal body.\n</content>", prompt.ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData("[\"kahvi\", \"halpa\"]", new[] { "kahvi", "halpa" })]
    [InlineData("```json\n[\"kahvi\", \"halpa\"]\n```", new[] { "kahvi", "halpa" })]
    [InlineData("[\"only one\"]", new[] { "coffee", "cheap" })]      // wrong count → originals
    [InlineData("not json", new[] { "coffee", "cheap" })]
    [InlineData("[\"kahvi\", \"\"]", new[] { "coffee", "cheap" })]   // blank entry → originals
    public void ParseTermList_ReadsJsonArrayOrFallsBack(string output, string[] expected) =>
        Assert.Equal(expected, ContentPrompt.ParseTermList(output, ["coffee", "cheap"]));

    [Fact]
    public void BuildTermTranslationPrompt_KeepsNonAsciiReadable()
    {
        var prompt = ContentPrompt.BuildTermTranslationPrompt(["pienyrittäjä"], "English");

        Assert.Contains("[\"pienyrittäjä\"]", prompt);
        Assert.Contains("into English", prompt);
    }
}
