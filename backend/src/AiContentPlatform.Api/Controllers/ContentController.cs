using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AiContentPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContentController : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IAiTextService _aiTextService;
    private readonly ILogger<ContentController> _logger;

    public ContentController(IAiTextService aiTextService, ILogger<ContentController> logger)
    {
        _aiTextService = aiTextService;
        _logger = logger;
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

    /// <summary>
    /// Streams generation as Server-Sent Events:
    /// <c>delta</c> events carry <c>{"text": "..."}</c> chunks of Markdown as they are written,
    /// followed by one <c>done</c> event with the full <see cref="GenerateContentResponse"/>,
    /// or an <c>error</c> event with <c>{"message": "..."}</c> if the provider fails mid-stream.
    /// </summary>
    [HttpPost("generate/stream")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IResult GenerateStream([FromBody] GenerateContentRequest request, CancellationToken cancellationToken) =>
        TypedResults.ServerSentEvents(StreamEvents(request, cancellationToken));

    private async IAsyncEnumerable<SseItem<string>> StreamEvents(GenerateContentRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        await using var chunks = _aiTextService.StreamContentAsync(request, cancellationToken).GetAsyncEnumerator(cancellationToken);

        while (true)
        {
            // C# can't yield inside a catch block, so capture provider errors and report them as an event.
            string? error = null;
            try
            {
                if (!await chunks.MoveNextAsync()) break;
            }
            catch (AiProviderException ex)
            {
                _logger.LogWarning(ex, "AI provider {Provider} failed while streaming", ex.Provider);
                error = ex.Message;
            }

            if (error is not null)
            {
                yield return Event("error", new { message = error });
                yield break;
            }

            output.Append(chunks.Current);
            yield return Event("delta", new { text = chunks.Current });
        }

        if (string.IsNullOrWhiteSpace(output.ToString()))
        {
            yield return Event("error", new { message = $"{_aiTextService.ProviderName}: Empty response." });
            yield break;
        }

        yield return Event("done", _aiTextService.BuildResponse(request, output.ToString()));
    }

    // Data is serialized here so it is always single-line JSON, whatever the payload type.
    private static SseItem<string> Event<T>(string type, T data) =>
        new(JsonSerializer.Serialize(data, Json), type);
}
