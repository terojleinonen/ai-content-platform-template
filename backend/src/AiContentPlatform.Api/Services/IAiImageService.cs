using AiContentPlatform.Api.Dtos;

namespace AiContentPlatform.Api.Services;

public interface IAiImageService
{
    string Name { get; }

    Task<GenerateImageResponse> GenerateImageAsync(GenerateImageRequest request, CancellationToken cancellationToken = default);
}
