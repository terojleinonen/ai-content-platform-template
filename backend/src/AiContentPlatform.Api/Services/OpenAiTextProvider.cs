using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiContentPlatform.Api.Options;
using Microsoft.Extensions.Options;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Text generation via the streaming OpenAI Chat Completions API. Also works with OpenAI-compatible
/// endpoints (Azure OpenAI proxies, local servers) by changing Ai:OpenAI:BaseUrl.
/// </summary>
public class OpenAiTextProvider : LlmTextProvider
{
    private readonly HttpClient _http;
    private readonly OpenAiOptions _options;

    public OpenAiTextProvider(HttpClient http, IOptions<AiOptions> options)
    {
        _http = http;
        _options = options.Value.OpenAI;
    }

    public override string Name => AiProviderNames.OpenAI;

    protected override async IAsyncEnumerable<string> StreamCompletionAsync(string systemPrompt, string userPrompt, UsageMeter usage, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var payload = new ChatRequest(_options.Model,
        [
            new ChatMessage("system", systemPrompt),
            new ChatMessage("user", userPrompt)
        ], Stream: true, new StreamOptions(IncludeUsage: true));

        usage.Model = _options.Model;
        using var response = await SendAsync(payload, cancellationToken);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        await foreach (var item in SseParser.Create(stream).EnumerateAsync(cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(item.Data)) continue;
            if (item.Data == "[DONE]") yield break;

            var chunk = JsonSerializer.Deserialize<ChatChunk>(item.Data);
            if (chunk?.Error is { } error)
            {
                throw new AiProviderException(Name, error.Message ?? "Stream error.");
            }

            // With include_usage, the last chunk has empty choices and the token counts.
            if (chunk?.Usage is { } reported)
            {
                usage.InputTokens = reported.PromptTokens;
                usage.OutputTokens = reported.CompletionTokens;
                usage.OutputFinal = true;
            }

            var text = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (!string.IsNullOrEmpty(text))
            {
                yield return text;
            }
        }
    }

    private async Task<HttpResponseMessage> SendAsync(ChatRequest payload, CancellationToken cancellationToken)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = JsonContent.Create(payload)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

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

    private record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] ChatMessage[] Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("stream_options")] StreamOptions StreamOptions);

    private record StreamOptions([property: JsonPropertyName("include_usage")] bool IncludeUsage);

    private record ChunkUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens);

    private record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string? Content);

    private record ChatChunk(
        [property: JsonPropertyName("choices")] Choice[]? Choices,
        [property: JsonPropertyName("error")] ChunkError? Error,
        [property: JsonPropertyName("usage")] ChunkUsage? Usage);

    private record Choice([property: JsonPropertyName("delta")] ChatMessage? Delta);

    private record ChunkError([property: JsonPropertyName("message")] string? Message);
}
