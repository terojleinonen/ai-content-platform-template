using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AiContentPlatform.Api.Tests;

/// <summary>Writes a fixed Finnish text and translates terms with a small dictionary.</summary>
internal sealed class FakeFinnishProvider : ITextGenerationProvider
{
    public static int TranslateCalls;
    public static bool FailTranslation;
    public static GenerateContentRequest? LastRequest;

    private static readonly Dictionary<string, string> Dictionary = new(StringComparer.OrdinalIgnoreCase)
    {
        ["small business owner"] = "pienyrittäjä",
        ["blog"] = "blogi",
        ["cheap"] = "halpa",
        ["freshly roasted"] = "tuoreeltaan paahdettu"
    };

    private const string Text = """
        # Miksi pienyrittäjän kannattaa blogata

        Pienyrittäjän arki on kiireinen. Blogi tuo asiakkaita, ja moni pienyrittäjä kirjoittaa blogia joka viikko.
        Halvalla ei kannata kilpailla.
        """;

    public string Name => "Fake";

    public IAsyncEnumerable<string> StreamContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default)
    {
        LastRequest = request;
        return Stream(cancellationToken);
    }

    public IAsyncEnumerable<string> StreamTransformAsync(TransformContentRequest request, CancellationToken cancellationToken = default) => Stream(cancellationToken);

    public Task<IReadOnlyList<string>> TranslateTermsAsync(IReadOnlyList<string> terms, string language, UsageMeter usage, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref TranslateCalls);
        if (FailTranslation) throw new AiProviderException(Name, "translation failed");
        return Task.FromResult<IReadOnlyList<string>>(terms.Select(t => Dictionary.GetValueOrDefault(t, t)).ToList());
    }

    private static async IAsyncEnumerable<string> Stream([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();
        yield return Text;
    }
}

[Collection(nameof(FakeFinnishProvider))]
public class TermTranslationTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public TermTranslationTests(ApiFactory factory)
    {
        _client = factory
            .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddTransient<ITextGenerationProvider, FakeFinnishProvider>()))
            .CreateUserClientAsync().GetAwaiter().GetResult();
        FakeFinnishProvider.FailTranslation = false;
    }

    private async Task<GenerateContentResponse> GenerateAsync(string[] keywords, Guid? projectId = null)
    {
        var response = await _client.PostAsJsonAsync("/api/content/generate", new GenerateContentRequest
        {
            Prompt = "Why small business owners should blog",
            Language = "Finnish",
            Keywords = keywords,
            ProjectId = projectId
        }, Json, Ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GenerateContentResponse>(Json, Ct))!;
    }

    [Fact]
    public async Task FinnishGeneration_ScoresTranslatedKeywordsIncludingInflectedForms()
    {
        var result = await GenerateAsync(["small business owner", "blog"]);

        Assert.Equal("pienyrittäjä", result.TermTranslations!["small business owner"]);
        // pienyrittäjän ×2 (title has its own count) + pienyrittäjä in the body; blogi + blogia.
        Assert.True(result.KeywordScores!["pienyrittäjä"] > 0.1);
        Assert.True(result.KeywordScores["blogi"] > 0.05);
        Assert.False(result.KeywordScores.ContainsKey("blog"));
    }

    [Fact]
    public async Task FinnishGeneration_BrandCheckFlagsTranslatedAvoidedTerm()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects", new SaveProjectRequest { Name = "Fi brand" }, Json, Ct))
            .Content.ReadFromJsonAsync<ProjectDto>(Json, Ct);
        await _client.PutAsJsonAsync($"/api/projects/{project!.Id}/brand-voice", new SaveBrandVoiceRequest
        {
            PreferredTerms = ["freshly roasted"],
            AvoidTerms = ["cheap"]
        }, Json, Ct);

        var result = await GenerateAsync([], project.Id);

        // "Halvalla" is an inflected form of "halpa" (cheap) with consonant gradation.
        Assert.Equal(["halpa"], result.BrandCheck!.AvoidTermsFound);
        Assert.Equal(["tuoreeltaan paahdettu"], result.BrandCheck.PreferredTermsMissing);
        Assert.Equal("halpa", result.TermTranslations!["cheap"]);
    }

    [Fact]
    public async Task FinnishGeneration_PromptUsesTranslatedTerms()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects", new SaveProjectRequest { Name = "Fi prompt" }, Json, Ct))
            .Content.ReadFromJsonAsync<ProjectDto>(Json, Ct);
        await _client.PutAsJsonAsync($"/api/projects/{project!.Id}/brand-voice", new SaveBrandVoiceRequest
        {
            PreferredTerms = ["freshly roasted"],
            AvoidTerms = ["cheap"]
        }, Json, Ct);

        await GenerateAsync(["blog"], project.Id);

        var sent = FakeFinnishProvider.LastRequest!;
        Assert.Equal(["blogi"], sent.Keywords!);
        Assert.Equal(["tuoreeltaan paahdettu"], sent.Brand!.Voice.PreferredTerms);
        Assert.Equal(["cheap", "halpa"], sent.Brand.Voice.AvoidTerms);

        var prompt = ContentPrompt.BuildUserPrompt(sent);
        Assert.Contains("SEO keywords: blogi", prompt);
        Assert.Contains("Never use these words or phrases: cheap, halpa", prompt);
    }

    [Fact]
    public async Task TermTranslations_AreCached()
    {
        await GenerateAsync(["blog"]);
        var calls = FakeFinnishProvider.TranslateCalls;

        await GenerateAsync(["blog"]);

        Assert.Equal(calls, FakeFinnishProvider.TranslateCalls);
    }

    [Fact]
    public async Task FailedTranslation_FallsBackToOriginalTerms()
    {
        FakeFinnishProvider.FailTranslation = true;

        var result = await GenerateAsync(["a term nobody translated before"]);

        Assert.Null(result.TermTranslations);
        Assert.Equal(0, result.KeywordScores!["a term nobody translated before"]);
    }

    [Fact]
    public async Task EnglishGeneration_DoesNotTranslateTerms()
    {
        var calls = FakeFinnishProvider.TranslateCalls;

        var response = await _client.PostAsJsonAsync("/api/content/generate",
            new GenerateContentRequest { Prompt = "x", Language = "English", Keywords = ["something new"] }, Json, Ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal(calls, FakeFinnishProvider.TranslateCalls);
    }
}

[CollectionDefinition(nameof(FakeFinnishProvider), DisableParallelization = true)]
public class FakeFinnishProviderCollection;
