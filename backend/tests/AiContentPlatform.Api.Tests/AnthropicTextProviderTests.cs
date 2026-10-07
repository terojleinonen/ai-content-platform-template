using System.Net;
using System.Text.Json;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Options;
using AiContentPlatform.Api.Services;

namespace AiContentPlatform.Api.Tests;

public class AnthropicTextProviderTests
{
    private const string Stream = """
        event: message_start
        data: {"type":"message_start","message":{"id":"msg_1","type":"message","role":"assistant","content":[],"usage":{"input_tokens":120,"cache_read_input_tokens":30,"output_tokens":1}}}

        event: content_block_start
        data: {"type":"content_block_start","index":0,"content_block":{"type":"text","text":""}}

        event: ping
        data: {"type": "ping"}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"# Hello"}}

        event: content_block_delta
        data: {"type":"content_block_delta","index":0,"delta":{"type":"text_delta","text":"\n\nWorld body."}}

        event: content_block_stop
        data: {"type":"content_block_stop","index":0}

        event: message_delta
        data: {"type":"message_delta","delta":{"stop_reason":"end_turn"},"usage":{"output_tokens":7}}

        event: message_stop
        data: {"type":"message_stop"}


        """;

    [Fact]
    public async Task StreamAsync_SendsStreamingRequestAndYieldsTextDeltas()
    {
        var handler = new StubHandler(HttpStatusCode.OK, Stream, "text/event-stream");
        var provider = CreateProvider(handler);

        var generateRequest = new GenerateContentRequest { Prompt = "Say hello" };
        var chunks = await provider.StreamContentAsync(generateRequest, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["# Hello", "\n\nWorld body."], chunks);
        var usage = generateRequest.Usage;
        Assert.Equal("test-model", usage.Model);
        Assert.Equal(120, usage.InputTokens);
        Assert.Equal(30, usage.CacheReadTokens);
        Assert.Equal(7, usage.OutputTokens); // final count from message_delta
        Assert.False(usage.Estimated);

        var request = handler.LastRequest!;
        Assert.Equal("https://api.test/v1/messages", request.RequestUri!.ToString());
        Assert.Equal("test-key", request.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", request.Headers.GetValues("anthropic-version").Single());

        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("test-model", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(512, body.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.True(body.RootElement.GetProperty("stream").GetBoolean());
        Assert.Contains("Say hello", body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task StreamAsync_ThrowsOnErrorEvent()
    {
        const string errorStream = """
            event: error
            data: {"type":"error","error":{"type":"overloaded_error","message":"Overloaded"}}


            """;
        var provider = CreateProvider(new StubHandler(HttpStatusCode.OK, errorStream, "text/event-stream"));

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.StreamContentAsync(new GenerateContentRequest { Prompt = "x" }, TestContext.Current.CancellationToken)
                .ToListAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Overloaded", ex.Message);
    }

    [Fact]
    public async Task StreamAsync_ThrowsOnHttpError()
    {
        var provider = CreateProvider(new StubHandler(HttpStatusCode.Unauthorized, """{"error":"invalid x-api-key"}"""));

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.StreamContentAsync(new GenerateContentRequest { Prompt = "x" }, TestContext.Current.CancellationToken)
                .ToListAsync(TestContext.Current.CancellationToken));

        Assert.Contains("401", ex.Message);
    }

    private static AnthropicTextProvider CreateProvider(StubHandler handler)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AiOptions
        {
            Anthropic = { ApiKey = "test-key", Model = "test-model", MaxTokens = 512 }
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") };
        return new AnthropicTextProvider(http, options);
    }
}
