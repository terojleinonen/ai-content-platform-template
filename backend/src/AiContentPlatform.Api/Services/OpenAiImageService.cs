using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Options;
using Microsoft.Extensions.Options;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Image generation via the OpenAI Images API.
/// </summary>
public class OpenAiImageService : IAiImageService
{
    private readonly HttpClient _http;
    private readonly OpenAiOptions _options;

    public OpenAiImageService(HttpClient http, IOptions<AiOptions> options)
    {
        _http = http;
        _options = options.Value.OpenAI;
    }

    public string Name => AiProviderNames.OpenAI;

    public async Task<GenerateImageResponse> GenerateImageAsync(GenerateImageRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = string.IsNullOrWhiteSpace(request.Style)
            ? request.Prompt.Trim()
            : $"{request.Prompt.Trim()}. Style: {request.Style.Trim()}.";

        using var message = new HttpRequestMessage(HttpMethod.Post, "v1/images/generations")
        {
            Content = JsonContent.Create(new ImageRequest(_options.ImageModel, prompt, 1, ToSupportedSize(request.Width, request.Height)))
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

            var result = await response.Content.ReadFromJsonAsync<ImageResponse>(cancellationToken);
            var image = result?.Data?.FirstOrDefault();
            var url = image?.Url ?? (image?.B64Json is { } b64 ? "data:image/png;base64," + b64 : null);
            if (url is null)
            {
                throw new AiProviderException(Name, "Response contained no image.");
            }

            return new GenerateImageResponse { Url = url, PromptUsed = image?.RevisedPrompt ?? prompt, Provider = Name };
        }
    }

    // gpt-image-1 supports square, landscape and portrait sizes only.
    private static string ToSupportedSize(int width, int height) =>
        ((double)width / height) switch
        {
            > 1.2 => "1536x1024",
            < 0.83 => "1024x1536",
            _ => "1024x1024"
        };

    private record ImageRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("n")] int N,
        [property: JsonPropertyName("size")] string Size);

    private record ImageResponse([property: JsonPropertyName("data")] ImageData[]? Data);

    private record ImageData(
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("b64_json")] string? B64Json,
        [property: JsonPropertyName("revised_prompt")] string? RevisedPrompt);
}
