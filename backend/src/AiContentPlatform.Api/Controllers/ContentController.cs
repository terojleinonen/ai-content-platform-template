using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiContentPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContentController : ControllerBase
{
    private readonly IAiTextService _aiTextService;

    public ContentController(IAiTextService aiTextService)
    {
        _aiTextService = aiTextService;
    }

    /// <summary>Generates content with the configured AI provider and scores it for SEO.</summary>
    [HttpPost("generate")]
    [ProducesResponseType<GenerateContentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<GenerateContentResponse>> Generate([FromBody] GenerateContentRequest request, CancellationToken cancellationToken)
    {
        var result = await _aiTextService.GenerateContentAsync(request, cancellationToken);
        return Ok(result);
    }
}
