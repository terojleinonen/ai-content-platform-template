using System.Net;
using System.Text.Json;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Options;
using AiContentPlatform.Api.Services;
using Anthropic;

namespace AiContentPlatform.Api.Tests;

/// <summary>
/// Runs the SDK-based provider against recorded Messages API streams, checking both what it
/// sends on the wire and how it reads text, usage, refusals and errors.
/// </summary>
public class AnthropicTextProviderTests
{
    private const string StreamTemplate = """
        event: message_start
        data: {"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","model":"MODEL","content":[],"stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":120,"cache_read_input_tokens":30,"cache_creation_input_tokens":0,"output_tokens":1}}}

        event: content_block_start
        data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

        event: ping
        data: {"type": "ping"}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"# Hello"}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"TEXT"}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":0}

        event: message_delta
        data: {"type":"message_delta","delta":{"stop_reason":"STOP","stop_sequence":nullDETAILS},"usage":{"output_tokens":7}}

        event: message_stop
        data: {"type":"message_stop"}


        """;

    private static string Stream(string model, string text, string stopReason = "end_turn", string? stopDetails = null) =>
        StreamTemplate
            .Replace("MODEL", model)
            .Replace("TEXT", text)
            .Replace("STOP", stopReason)
            .Replace("DETAILS", stopDetails is null ? "" : $",\"stop_details\":{stopDetails}");

    private static Task<List<string>> Run(AnthropicTextProvider provider, GenerateContentRequest request) =>
        provider.StreamContentAsync(request, TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task Streams_TextAndUsage_AndSendsTheExpectedRequest()
    {
        var handler = new StubHandler(HttpStatusCode.OK, Stream("claude-sonnet-5-5", "\\n\\nWorld body."), "text/event-stream");
        var provider = CreateProvider(handler);
        var request = new GenerateContentRequest { Prompt = "Say hello" };

        var chunks = await Run(provider, request);

        Assert.Equal(["# Hello", "\n\nWorld body."], chunks);
        Assert.Equal(("claude-sonnet-5-5", 120, 30, 7, true), (request.Usage.Model, request.Usage.InputTokens, request.Usage.CacheReadTokens, request.Usage.OutputTokens, request.Usage.OutputFinal));

        var sent = handler.LastRequest!;
        Assert.StartsWith("https://api.test/v1/messages", sent.RequestUri!.ToString());
        Assert.Equal("test-key", sent.Headers.GetValues("x-api-key").Single());
        Assert.Contains("server-side-fallback-2026-07-01", sent.Headers.GetValues("anthropic-beta"));

        using var body = JsonDocument.Parse(handler.LastBody!);
        var root = body.RootElement;
        Assert.Equal("claude-sonnet-5-5", root.GetProperty("model").GetString());
        Assert.Equal(512, root.GetProperty("max_tokens").GetInt32());
        Assert.True(root.GetProperty("stream").GetBoolean());
        Assert.Equal("default", root.GetProperty("fallbacks").GetString());
        Assert.Contains("content writer", root.GetProperty("system").GetString());
        Assert.Contains("Say hello", root.GetProperty("messages")[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Defaults_AreOpus55_MediumEffort_RoomForThinking_AndFallback()
    {
        var handler = new StubHandler(HttpStatusCode.OK, Stream("claude-opus-5-5", " text"), "text/event-stream");
        var options = Microsoft.Extensions.Options.Options.Create(new AiOptions { Anthropic = { ApiKey = "test-key" } });
        var provider = new AnthropicTextProvider(CreateClient(handler), options);

        await Run(provider, new GenerateContentRequest { Prompt = "x" });

        using var body = JsonDocument.Parse(handler.LastBody!);
        var root = body.RootElement;
        Assert.Equal("claude-opus-5-5", root.GetProperty("model").GetString());
        Assert.Equal(16000, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal("medium", root.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.Equal("default", root.GetProperty("fallbacks").GetString());
        Assert.False(root.TryGetProperty("thinking", out _)); // Opus 5.5 always thinks; disabling it is a 400
    }

    [Theory]
    [InlineData("low", "low")]
    [InlineData("HIGH", "high")]
    [InlineData("max", "max")]
    public async Task ConfiguredEffort_IsSent(string configured, string sent)
    {
        var handler = new StubHandler(HttpStatusCode.OK, Stream("claude-opus-5-5", " text"), "text/event-stream");

        await Run(CreateProvider(handler, effort: configured), new GenerateContentRequest { Prompt = "x" });

        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal(sent, body.RootElement.GetProperty("output_config").GetProperty("effort").GetString());
    }

    [Fact]
    public async Task UnknownEffort_IsRejected()
    {
        var provider = CreateProvider(new StubHandler(HttpStatusCode.OK, Stream("claude-opus-5-5", " text"), "text/event-stream"), effort: "extreme");

        await Assert.ThrowsAsync<InvalidOperationException>(() => Run(provider, new GenerateContentRequest { Prompt = "x" }));
    }

    [Fact]
    public async Task CutOffAtTokenLimit_IsReportedNotPassedOffAsComplete()
    {
        var provider = CreateProvider(new StubHandler(HttpStatusCode.OK, Stream("claude-opus-5-5", " half a sent", "max_tokens"), "text/event-stream"));

        var ex = await Assert.ThrowsAsync<AiProviderException>(() => Run(provider, new GenerateContentRequest { Prompt = "x" }));

        Assert.Contains("length limit", ex.Message);
    }

    [Fact]
    public async Task ServedByFallbackModel_IsRecordedForPricing()
    {
        var provider = CreateProvider(new StubHandler(HttpStatusCode.OK, Stream("claude-sonnet-5", " text"), "text/event-stream"));
        var request = new GenerateContentRequest { Prompt = "x" };

        await Run(provider, request);

        Assert.Equal("claude-sonnet-5", request.Usage.Model);
    }

    [Theory]
    [InlineData("claude-sonnet-5-5", false)] // switched off
    [InlineData("claude-haiku-4-5", true)]   // model without the "default" fallback form
    public async Task Fallback_IsOnlySentWhenEnabledAndSupported(string model, bool enabled)
    {
        var handler = new StubHandler(HttpStatusCode.OK, Stream(model, " text"), "text/event-stream");

        await Run(CreateProvider(handler, model, enabled), new GenerateContentRequest { Prompt = "x" });

        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.False(body.RootElement.TryGetProperty("fallbacks", out _));
        Assert.False(handler.LastRequest!.Headers.Contains("anthropic-beta") &&
                     handler.LastRequest.Headers.GetValues("anthropic-beta").Any(v => v.Contains("server-side-fallback")));
    }

    [Fact]
    public async Task Refusal_ThrowsWithCategory_EvenAfterPartialText()
    {
        var provider = CreateProvider(new StubHandler(HttpStatusCode.OK,
            Stream("claude-sonnet-5-5", " partial", "refusal", """{"type":"refusal","category":"cyber","explanation":null}"""), "text/event-stream"));
        var request = new GenerateContentRequest { Prompt = "x" };

        var ex = await Assert.ThrowsAsync<AiProviderException>(() => Run(provider, request));

        Assert.Contains("declined", ex.Message);
        Assert.Contains("cyber", ex.Message);
        Assert.Equal(7, request.Usage.OutputTokens); // the partial output is still billed and metered
    }

    [Fact]
    public async Task HttpError_BecomesProviderError()
    {
        var provider = CreateProvider(new StubHandler(HttpStatusCode.Unauthorized,
            """{"type":"error","error":{"type":"authentication_error","message":"invalid x-api-key"}}"""));

        var ex = await Assert.ThrowsAsync<AiProviderException>(() => Run(provider, new GenerateContentRequest { Prompt = "x" }));

        Assert.Contains("invalid x-api-key", ex.Message);
    }

    [Fact]
    public async Task ErrorEventMidStream_BecomesProviderError()
    {
        const string errorStream = """
            event: error
            data: {"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}


            """;
        var provider = CreateProvider(new StubHandler(HttpStatusCode.OK, errorStream, "text/event-stream"));

        var ex = await Assert.ThrowsAsync<AiProviderException>(() => Run(provider, new GenerateContentRequest { Prompt = "x" }));

        Assert.Contains("Overloaded", ex.Message);
    }

    private static AnthropicTextProvider CreateProvider(StubHandler handler, string model = "claude-sonnet-5-5", bool fallback = true, string effort = "medium")
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AiOptions
        {
            Anthropic = { ApiKey = "test-key", Model = model, MaxTokens = 512, ServerSideFallback = fallback, Effort = effort }
        });
        return new AnthropicTextProvider(CreateClient(handler), options);
    }

    private static AnthropicClient CreateClient(StubHandler handler) => new()
    {
        ApiKey = "test-key",
        BaseUrl = "https://api.test",
        HttpClient = new HttpClient(handler),
        MaxRetries = 0
    };
}
