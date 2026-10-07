using System.ComponentModel.DataAnnotations;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AiContentPlatform.Api.Data;
using AiContentPlatform.Api.Dtos;
using AiContentPlatform.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiContentPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContentController : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IAiTextService _aiTextService;
    private readonly AppDbContext _db;
    private readonly ILogger<ContentController> _logger;

    public ContentController(IAiTextService aiTextService, AppDbContext db, ILogger<ContentController> logger)
    {
        _aiTextService = aiTextService;
        _db = db;
        _logger = logger;
    }

    /// <summary>Generates content with the configured AI provider and scores it for SEO.</summary>
    [HttpPost("generate")]
    [ProducesResponseType<GenerateContentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GenerateContentResponse>> Generate([FromBody] GenerateContentRequest request, CancellationToken cancellationToken)
    {
        if (!await TryLoadBrandAsync(request.ProjectId, b => request.Brand = b, cancellationToken)) return ProjectNotFound();

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
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> GenerateStream([FromBody] GenerateContentRequest request, CancellationToken cancellationToken)
    {
        if (!await TryLoadBrandAsync(request.ProjectId, b => request.Brand = b, cancellationToken)) return ProjectNotFoundResult();

        return TypedResults.ServerSentEvents(StreamEvents(
            _aiTextService.StreamContentAsync(request, cancellationToken),
            output => _aiTextService.BuildResponse(output, request.Title, request.Keywords, request.Brand),
            cancellationToken));
    }

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
        if (!await TryLoadBrandAsync(request.ProjectId, b => request.Brand = b, cancellationToken)) return ProjectNotFound();

        var results = await _aiTextService.GenerateVariantsAsync(request, count, cancellationToken);
        return Ok(results);
    }

    /// <summary>Rewrites existing content: improve, shorten, expand, change tone, translate or a custom instruction.</summary>
    [HttpPost("transform")]
    [ProducesResponseType<GenerateContentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GenerateContentResponse>> Transform([FromBody] TransformContentRequest request, CancellationToken cancellationToken)
    {
        if (!await TryLoadBrandAsync(request.ProjectId, b => request.Brand = b, cancellationToken)) return ProjectNotFound();

        var result = await _aiTextService.TransformContentAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Streams a rewrite as Server-Sent Events, with the same events as <c>generate/stream</c>.</summary>
    [HttpPost("transform/stream")]
    [Produces("text/event-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IResult> TransformStream([FromBody] TransformContentRequest request, CancellationToken cancellationToken)
    {
        if (!await TryLoadBrandAsync(request.ProjectId, b => request.Brand = b, cancellationToken)) return ProjectNotFoundResult();

        return TypedResults.ServerSentEvents(StreamEvents(
            _aiTextService.StreamTransformAsync(request, cancellationToken),
            output => _aiTextService.BuildResponse(output, request.Title, request.Keywords, request.Brand),
            cancellationToken));
    }

    /// <summary>
    /// Loads the brand voice of <paramref name="projectId"/> (if any) into the request.
    /// Returns false when a project id was given but doesn't exist.
    /// </summary>
    private async Task<bool> TryLoadBrandAsync(Guid? projectId, Action<BrandContext?> setBrand, CancellationToken cancellationToken)
    {
        if (projectId is null) return true;

        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Name, p.BrandVoice })
            .FirstOrDefaultAsync(cancellationToken);
        if (project is null) return false;

        setBrand(project.BrandVoice is { IsEmpty: false } voice ? new BrandContext(project.Name, voice) : null);
        return true;
    }

    private ObjectResult ProjectNotFound() =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Project not found.");

    private static IResult ProjectNotFoundResult() =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Project not found.");

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
