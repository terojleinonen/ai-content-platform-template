using AiContentPlatform.Api.Options;
using Microsoft.AspNetCore.Authorization;
using AiContentPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AiContentPlatform.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly AiOptions _ai;

    public HealthController(IOptions<AiOptions> ai)
    {
        _ai = ai.Value;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var textProvider = AiProviderSelector.ResolveText(_ai);
        return Ok(new
        {
            status = "ok",
            timestamp = DateTime.UtcNow,
            textProvider,
            textModel = textProvider switch
            {
                AiProviderNames.Anthropic => _ai.Anthropic.Model,
                AiProviderNames.OpenAI => _ai.OpenAI.Model,
                _ => "built-in templates"
            },
            imageProvider = AiProviderSelector.ResolveImage(_ai)
        });
    }
}
