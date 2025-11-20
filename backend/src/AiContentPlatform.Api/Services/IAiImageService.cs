using AiContentPlatform.Api.Dtos;

namespace AiContentPlatform.Api.Services;

public interface IAiImageService
{
    Task<GenerateImageResponse> GenerateImageAsync(GenerateImageRequest request, CancellationToken cancellationToken = default);
}
