using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Services;

namespace AiContentPlatform.Api.Tests;

public class ContentPromptTests
{
    [Fact]
    public void Parse_SplitsHeadingIntoTitle()
    {
        var result = ContentPrompt.Parse("# My Title\n\nFirst paragraph.\n\n## Section", new GenerateContentRequest());

        Assert.Equal("My Title", result.Title);
        Assert.Equal("First paragraph.\n\n## Section", result.Body);
    }

    [Fact]
    public void Parse_StripsCodeFences()
    {
        var result = ContentPrompt.Parse("```markdown\n# Fenced\nBody\n```", new GenerateContentRequest());

        Assert.Equal("Fenced", result.Title);
        Assert.Equal("Body", result.Body);
    }

    [Fact]
    public void Parse_FallsBackToRequestedTitle()
    {
        var result = ContentPrompt.Parse("Just a body.", new GenerateContentRequest { Title = "Requested" });

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
}
