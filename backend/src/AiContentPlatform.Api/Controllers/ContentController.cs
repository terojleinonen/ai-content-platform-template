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

    [HttpPost("generate")]
    public async Task<ActionResult<GenerateContentResponse>> Generate([FromBody] GenerateContentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("Prompt is required.");
        }

        var result = await _aiTextService.GenerateContentAsync(request, cancellationToken);
        return Ok(result);
    }
}
