using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Options;
using Microsoft.Extensions.Options;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Text generation via the Anthropic Messages API (https://docs.anthropic.com/en/api/messages).
/// </summary>
public class AnthropicTextProvider : ITextGenerationProvider
{
    private readonly HttpClient _http;
    private readonly AnthropicOptions _options;

    public AnthropicTextProvider(HttpClient http, IOptions<AiOptions> options)
    {
        _http = http;
        _options = options.Value.Anthropic;
    }

    public string Name => AiProviderNames.Anthropic;

    public async Task<GeneratedText> GenerateAsync(GenerateContentRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new MessagesRequest(
            _options.Model,
            _options.MaxTokens,
            ContentPrompt.SystemPrompt,
            [new Message("user", ContentPrompt.BuildUserPrompt(request))]);

        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/messages")
        {
            Content = JsonContent.Create(payload)
        };
        message.Headers.Add("x-api-key", _options.ApiKey);
        message.Headers.Add("anthropic-version", "2023-06-01");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(message, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException(Name, "Could not reach the API.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new AiProviderException(Name, $"HTTP {(int)response.StatusCode}: {Truncate(error)}");
            }

            var result = await response.Content.ReadFromJsonAsync<MessagesResponse>(cancellationToken);
            var text = string.Concat(result?.Content?.Where(c => c.Type == "text").Select(c => c.Text) ?? []);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new AiProviderException(Name, $"Empty response (stop_reason: {result?.StopReason ?? "unknown"}).");
            }

            return ContentPrompt.Parse(text, request);
        }
    }

    private static string Truncate(string s) => s.Length > 500 ? s[..500] + "…" : s;

    private record MessagesRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] Message[] Messages);

    private record Message(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private record MessagesResponse(
        [property: JsonPropertyName("content")] ContentBlock[]? Content,
        [property: JsonPropertyName("stop_reason")] string? StopReason);

    private record ContentBlock(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("text")] string? Text);
}
