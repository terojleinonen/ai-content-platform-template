using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Options;
using AiContentPlatform.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AiContentPlatform.Api.Tests;

public class UsageRecorderPricingTests
{
    private static UsageRecorder CreateRecorder()
    {
        var options = new AiOptions();
        options.Pricing["claude-sonnet-5-5"] = new ModelPrice { InputPerMTok = 2, OutputPerMTok = 10 };
        return new UsageRecorder(null!, new StaticOptionsMonitor<AiOptions>(options), NullLogger<UsageRecorder>.Instance);
    }

    [Fact]
    public void PriceOf_UsesPerMillionTokenPricesIncludingCache()
    {
        var meter = new UsageMeter
        {
            Model = "claude-sonnet-5-5",
            InputTokens = 1_000_000,
            OutputTokens = 100_000,
            CacheReadTokens = 1_000_000,   // 0.1x input price
            CacheWriteTokens = 1_000_000   // 1.25x input price
        };

        // 2 + 1 (output) + 0.2 (cache read) + 2.5 (cache write)
        Assert.Equal(5.7, CreateRecorder().PriceOf(meter));
    }

    [Theory]
    [InlineData("mock", 0.0)]
    [InlineData("gpt-4.1-mini", null)]
    [InlineData(null, null)]
    public void PriceOf_MockIsFreeAndUnknownModelsAreUnpriced(string? model, double? expected) =>
        Assert.Equal(expected, CreateRecorder().PriceOf(new UsageMeter { Model = model, InputTokens = 100 }));

    private sealed class StaticOptionsMonitor<T>(T value) : Microsoft.Extensions.Options.IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}

public class UsageApiTests : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client;
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public UsageApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Calls_AreRecordedAndReported()
    {
        var project = await (await _client.PostAsJsonAsync("/api/projects", new SaveProjectRequest { Name = "Usage project" }, Json, Ct))
            .Content.ReadFromJsonAsync<ProjectDto>(Json, Ct);

        var generated = await (await _client.PostAsJsonAsync("/api/content/generate",
            new GenerateContentRequest { Prompt = "usage test", ProjectId = project!.Id }, Json, Ct))
            .Content.ReadFromJsonAsync<GenerateContentResponse>(Json, Ct);
        await _client.PostAsJsonAsync("/api/content/transform",
            new TransformContentRequest { Action = TransformAction.Shorten, Title = "t", Body = "Some text. More text.", ProjectId = project.Id }, Json, Ct);
        await _client.PostAsJsonAsync("/api/content/variants?count=2", new GenerateContentRequest { Prompt = "usage test" }, Json, Ct);
        await _client.PostAsJsonAsync("/api/image/generate", new GenerateImageRequest { Prompt = "usage test" }, Json, Ct);

        // The response carries the usage of its own call.
        Assert.Equal("mock", generated!.Usage!.Model);
        Assert.True(generated.Usage.OutputTokens > 0);
        Assert.Equal(0, generated.Usage.CostUsd);

        var report = await _client.GetFromJsonAsync<UsageReport>("/api/usage?days=7", Json, Ct);

        Assert.True(report!.Totals.Calls >= 5);
        Assert.Equal(7, report.ByDay.Count);
        Assert.Contains(report.ByOperation, g => g.Key == "Variant" && g.Calls >= 2);
        Assert.Contains(report.ByOperation, g => g.Key == "Image");
        Assert.Contains(report.ByProject, g => g.Label == "Usage project" && g.Calls == 2);
        Assert.Contains(report.Recent, r => r.Operation == AiOperation.Transform && r.Detail == "Shorten" && r.Status == AiCallStatus.Succeeded);
    }

    [Fact]
    public async Task UsageReport_RejectsOutOfRangeDays()
    {
        var response = await _client.GetAsync("/api/usage?days=0", Ct);

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}
