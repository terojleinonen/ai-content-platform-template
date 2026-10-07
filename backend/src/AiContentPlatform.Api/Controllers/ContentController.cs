using System.ComponentModel.DataAnnotations;
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
        TypedResults.ServerSentEvents(StreamEvents(
            _aiTextService.StreamContentAsync(request, cancellationToken),
            output => _aiTextService.BuildResponse(output, request.Title, request.Keywords),
            cancellationToken));

    /// <summary>Generates several alternative versions of the same brief (2–4) in parallel, each from a different angle.</summary>
    [HttpPost("variants")]
    [ProducesResponseType<IReadOnlyList<GenerateContentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IReadOnlyList<GenerateContentResponse>>> Variants(
        [FromBody] GenerateContentRequest request,
        CancellationToken cancellationToken,
        [FromQuery, Range(2, 4)] int count = 3)
    {
        var results = await _aiTextService.GenerateVariantsAsync(request, count, cancellationToken);
        return Ok(results);
    }

    /// <summary>Rewrites existing content: improve, shorten, expand, change tone, translate or a custom instruction.</summary>
    [HttpPost("transform")]
    [ProducesResponseType<GenerateContentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<GenerateContentResponse>> Transform([FromBody] TransformContentRequest request, CancellationToken cancellationToken)
    {
        var result = await _aiTextService.TransformContentAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Streams a rewrite as Server-Sent Events, with the same events as <c>generate/stream</c>.</summary>
    [HttpPost("transform/stream")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IResult TransformStream([FromBody] TransformContentRequest request, CancellationToken cancellationToken) =>
        TypedResults.ServerSentEvents(StreamEvents(
            _aiTextService.StreamTransformAsync(request, cancellationToken),
            output => _aiTextService.BuildResponse(output, request.Title, request.Keywords),
            cancellationToken));

    private async IAsyncEnumerable<SseItem<string>> StreamEvents(
        IAsyncEnumerable<string> source,
        Func<string, GenerateContentResponse> buildResponse,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        await using var chunks = source.GetAsyncEnumerator(cancellationToken);

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

        yield return Event("done", buildResponse(output.ToString()));
    }

    // Data is serialized here so it is always single-line JSON, whatever the payload type.
    private static SseItem<string> Event<T>(string type, T data) =>
        new(JsonSerializer.Serialize(data, Json), type);
}
