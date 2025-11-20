using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiContentPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImageController : ControllerBase
{
    private readonly IAiImageService _aiImageService;

    public ImageController(IAiImageService aiImageService)
    {
        _aiImageService = aiImageService;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<GenerateImageResponse>> Generate([FromBody] GenerateImageRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Prompt is required.");
        }

        var result = await _aiImageService.GenerateImageAsync(request, cancellationToken);
        return Ok(result);
    }
}
