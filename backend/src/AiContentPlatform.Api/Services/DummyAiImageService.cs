using AiContentPlatform.Api.Dtos;

namespace AiContentPlatform.Api.Services;

/// <summary>
/// Dummy image service that simulates image generation.
/// Replace with calls to a real image generation provider.
/// </summary>
public class DummyAiImageService : IAiImageService
{
    public Task<GenerateImageResponse> GenerateImageAsync(GenerateImageRequest request, CancellationToken cancellationToken = default)
    {
        var url = "https://via.placeholder.com/1024x1024.png?text=AI+Image"; // placeholder

        var response = new GenerateImageResponse
        {
            Url = url,
            PromptUsed = request.Prompt
        };

        return Task.FromResult(response);
    }
}
