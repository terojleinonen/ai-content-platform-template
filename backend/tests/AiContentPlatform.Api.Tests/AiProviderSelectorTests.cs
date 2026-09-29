using AiContentPlatform.Api.Options;
using AiContentPlatform.Api.Services;

namespace AiContentPlatform.Api.Tests;

public class AiProviderSelectorTests
{
    [Fact]
    public void Auto_WithoutKeys_UsesMock()
    {
        var options = new AiOptions();

        Assert.Equal(AiProviderNames.Mock, AiProviderSelector.ResolveText(options));
        Assert.Equal(AiProviderNames.Mock, AiProviderSelector.ResolveImage(options));
    }

    [Fact]
    public void Auto_PrefersAnthropicForTextAndOpenAiForImages()
    {
        var options = new AiOptions
        {
            Anthropic = { ApiKey = "a" },
            OpenAI = { ApiKey = "o" }
        };

        Assert.Equal(AiProviderNames.Anthropic, AiProviderSelector.ResolveText(options));
        Assert.Equal(AiProviderNames.OpenAI, AiProviderSelector.ResolveImage(options));
    }

    [Fact]
    public void ExplicitProvider_WithoutKey_Throws()
    {
        var options = new AiOptions { TextProvider = "Anthropic" };

        Assert.Throws<InvalidOperationException>(() => AiProviderSelector.ResolveText(options));
    }

    [Fact]
    public void ExplicitMock_IgnoresKeys()
    {
        var options = new AiOptions { TextProvider = "mock", Anthropic = { ApiKey = "a" } };

        Assert.Equal(AiProviderNames.Mock, AiProviderSelector.ResolveText(options));
    }
}
