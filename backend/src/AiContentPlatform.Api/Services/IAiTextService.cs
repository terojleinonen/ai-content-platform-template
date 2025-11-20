using AiContentPlatform.Api.Dtos;

namespace AiContentPlatform.Api.Services;

public interface IAiTextService
{
    Task<GenerateContentResponse> GenerateContentAsync(GenerateContentRequest request, CancellationToken cancellationToken = default);
}
