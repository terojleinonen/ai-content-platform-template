using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Options;
using Microsoft.Extensions.Options;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Text generation via the OpenAI Chat Completions API. Also works with OpenAI-compatible
/// endpoints (Azure OpenAI proxies, local servers) by changing Ai:OpenAI:BaseUrl.
/// </summary>
public class OpenAiTextProvider : ITextGenerationProvider
{
    private readonly HttpClient _http;
    private readonly OpenAiOptions _options;

    public OpenAiTextProvider(HttpClient http, IOptions<AiOptions> options)
    {
        _http = http;
        _options = options.Value.OpenAI;
    }

    public string Name => AiProviderNames.OpenAI;

    public async Task<GeneratedText> GenerateAsync(GenerateContentRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new ChatRequest(_options.Model,
        [
            new ChatMessage("system", ContentPrompt.SystemPrompt),
            new ChatMessage("user", ContentPrompt.BuildUserPrompt(request))
        ]);

        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions")
        {
            Content = JsonContent.Create(payload)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

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
                throw new AiProviderException(Name, $"HTTP {(int)response.StatusCode}: {(error.Length > 500 ? error[..500] + "…" : error)}");
            }

            var result = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
            var text = result?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new AiProviderException(Name, "Empty response.");
            }

            return ContentPrompt.Parse(text, request);
        }
    }

    private record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] ChatMessage[] Messages);

    private record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string? Content);

    private record ChatResponse([property: JsonPropertyName("choices")] Choice[]? Choices);

    private record Choice([property: JsonPropertyName("message")] ChatMessage? Message);
}
