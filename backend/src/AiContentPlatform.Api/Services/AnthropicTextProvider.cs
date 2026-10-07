using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiContentPlatform.Api.Options;
using Microsoft.Extensions.Options;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Text generation via the streaming Anthropic Messages API (https://docs.anthropic.com/en/api/messages-streaming).
/// </summary>
public class AnthropicTextProvider : LlmTextProvider
{
    private readonly HttpClient _http;
    private readonly AnthropicOptions _options;

    public AnthropicTextProvider(HttpClient http, IOptions<AiOptions> options)
    {
        _http = http;
        _options = options.Value.Anthropic;
    }

    public override string Name => AiProviderNames.Anthropic;

    protected override async IAsyncEnumerable<string> StreamCompletionAsync(string systemPrompt, string userPrompt, UsageMeter usage, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var payload = new MessagesRequest(
            _options.Model,
            _options.MaxTokens,
            systemPrompt,
            [new Message("user", userPrompt)],
            Stream: true);

        usage.Model = _options.Model;
        using var response = await SendAsync(payload, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        await foreach (var item in SseParser.Create(stream).EnumerateAsync(cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(item.Data)) continue;

            var streamEvent = JsonSerializer.Deserialize<StreamEvent>(item.Data);
            switch (streamEvent?.Type)
            {
                // Input (and cache) tokens arrive up front, the final output count with message_delta.
                case "message_start" when streamEvent.Message?.Usage is { } start:
                    usage.InputTokens = start.InputTokens;
                    usage.CacheWriteTokens = start.CacheCreationInputTokens ?? 0;
                    usage.CacheReadTokens = start.CacheReadInputTokens ?? 0;
                    usage.OutputTokens = start.OutputTokens ?? 0;
                    break;
                case "message_delta" when streamEvent.Usage?.OutputTokens is { } outputTokens:
                    usage.OutputTokens = outputTokens;
                    usage.OutputFinal = true;
                    break;
                case "content_block_delta" when streamEvent.Delta?.Type == "text_delta" && !string.IsNullOrEmpty(streamEvent.Delta.Text):
                    yield return streamEvent.Delta.Text;
                    break;
                case "error":
                    throw new AiProviderException(Name, streamEvent.Error?.Message ?? "Stream error.");
                case "message_stop":
                    yield break;
            }
        }
    }

    private async Task<HttpResponseMessage> SendAsync(MessagesRequest payload, CancellationToken cancellationToken)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = JsonContent.Create(payload)
        };
        message.Headers.Add("x-api-key", _options.ApiKey);
        message.Headers.Add("anthropic-version", "2023-06-01");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException(Name, "Could not reach the API.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();
            throw new AiProviderException(Name, $"HTTP {(int)response.StatusCode}: {(error.Length > 500 ? error[..500] + "…" : error)}");
        }

        return response;
    }

    private record MessagesRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] Message[] Messages,
        [property: JsonPropertyName("stream")] bool Stream);

    private record Message(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private record StreamEvent(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("delta")] Delta? Delta,
        [property: JsonPropertyName("error")] StreamError? Error,
        [property: JsonPropertyName("message")] StartMessage? Message,
        [property: JsonPropertyName("usage")] Usage? Usage);

    private record StartMessage([property: JsonPropertyName("usage")] Usage? Usage);

    private record Usage(
        [property: JsonPropertyName("input_tokens")] int InputTokens,
        [property: JsonPropertyName("output_tokens")] int? OutputTokens,
        [property: JsonPropertyName("cache_creation_input_tokens")] int? CacheCreationInputTokens,
        [property: JsonPropertyName("cache_read_input_tokens")] int? CacheReadInputTokens);

    private record Delta(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("text")] string? Text);

    private record StreamError([property: JsonPropertyName("message")] string? Message);
}
