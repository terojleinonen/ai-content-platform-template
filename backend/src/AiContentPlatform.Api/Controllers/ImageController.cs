using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Options;
using AiContentPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AiContentPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImageController : ControllerBase
{
    private readonly IAiImageService _aiImageService;
    private readonly IUsageRecorder _usage;
    private readonly AiOptions _options;

    public ImageController(IAiImageService aiImageService, IUsageRecorder usage, IOptions<AiOptions> options)
    {
        _aiImageService = aiImageService;
        _usage = usage;
        _options = options.Value;
    }

    [HttpPost("generate")]
    [ProducesResponseType<GenerateImageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<GenerateImageResponse>> Generate([FromBody] GenerateImageRequest request, CancellationToken cancellationToken)
    {
        // Images are priced per image, not per token: recorded as calls without a cost estimate.
        var meter = new UsageMeter
        {
            Model = _aiImageService.Name == AiProviderNames.Mock ? UsageRecorder.MockModel : _options.OpenAI.ImageModel
        };
        var result = await _usage.TrackAsync(new UsageContext(AiOperation.Image), _aiImageService.Name, meter,
            () => _aiImageService.GenerateImageAsync(request, cancellationToken));
        return Ok(result);
    }
}
