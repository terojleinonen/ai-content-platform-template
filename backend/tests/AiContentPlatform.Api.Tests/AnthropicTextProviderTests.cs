using System.Net;
using System.Text;
using System.Text.Json;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Options;
using AiContentPlatform.Api.Services;
using Microsoft.Extensions.Options;

namespace AiContentPlatform.Api.Tests;

public class AnthropicTextProviderTests
{
    [Fact]
    public async Task GenerateAsync_SendsMessagesRequestAndParsesResponse()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"content":[{"type":"text","text":"# Hello\n\nWorld body."}],"stop_reason":"end_turn"}""");
        var provider = CreateProvider(handler);

        var result = await provider.GenerateAsync(new GenerateContentRequest { Prompt = "Say hello" }, TestContext.Current.CancellationToken);

        Assert.Equal("Hello", result.Title);
        Assert.Equal("World body.", result.Body);

        var request = handler.LastRequest!;
        Assert.Equal("https://api.test/v1/messages", request.RequestUri!.ToString());
        Assert.Equal("test-key", request.Headers.GetValues("x-api-key").Single());
        Assert.Equal("2023-06-01", request.Headers.GetValues("anthropic-version").Single());

        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("test-model", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(512, body.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Contains("Say hello", body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task GenerateAsync_ThrowsAiProviderExceptionOnHttpError()
    {
        var provider = CreateProvider(new StubHandler(HttpStatusCode.Unauthorized, """{"error":"invalid x-api-key"}"""));

        var ex = await Assert.ThrowsAsync<AiProviderException>(() =>
            provider.GenerateAsync(new GenerateContentRequest { Prompt = "x" }, TestContext.Current.CancellationToken));

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

    private sealed class StubHandler(HttpStatusCode status, string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
