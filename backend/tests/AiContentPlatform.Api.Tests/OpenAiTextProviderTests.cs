using System.Net;
using System.Text.Json;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Options;
using AiContentPlatform.Api.Services;

namespace AiContentPlatform.Api.Tests;

public class OpenAiTextProviderTests
{
    [Fact]
    public async Task StreamAsync_YieldsContentDeltasUntilDone()
    {
        const string stream = """
            data: {"choices":[{"index":0,"delta":{"role":"assistant","content":""}}]}

            data: {"choices":[{"index":0,"delta":{"content":"# Hi"}}]}

            data: {"choices":[{"index":0,"delta":{"content":"\n\nBody"}}]}

            data: {"choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

            data: [DONE]


            """;
        var handler = new StubHandler(HttpStatusCode.OK, stream, "text/event-stream");
        var options = Microsoft.Extensions.Options.Options.Create(new AiOptions { OpenAI = { ApiKey = "k", Model = "m" } });
        var provider = new OpenAiTextProvider(new HttpClient(handler) { BaseAddress = new Uri("https://api.test/") }, options);

        var chunks = await provider.StreamContentAsync(new GenerateContentRequest { Prompt = "x" }, TestContext.Current.CancellationToken)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["# Hi", "\n\nBody"], chunks);
        Assert.Equal("Bearer k", handler.LastRequest!.Headers.Authorization!.ToString());
        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.True(body.RootElement.GetProperty("stream").GetBoolean());
    }
}
