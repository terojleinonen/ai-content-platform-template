using AiContentPlatform.Api.Auth;
using AiContentPlatform.Api.Data;
using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiContentPlatform.Api.Controllers;

[ApiController]
[Route("api/content-items")]
public class ContentItemsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;

    public ContentItemsController(AppDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    // Items of the signed-in user's projects only; others are reported as not found.
    private IQueryable<ContentItem> MyItems => _db.ContentItems.Where(c => c.Project!.OwnerId == _user.RequiredId);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContentItemDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await MyItems.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return item is null ? NotFound() : ContentItemDto.From(item);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ContentItemDto>> Update(Guid id, [FromBody] SaveContentItemRequest request, CancellationToken cancellationToken)
    {
        var item = await MyItems.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (item is null) return NotFound();

        Apply(item, request);
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return ContentItemDto.From(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await MyItems.Where(c => c.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? NotFound() : NoContent();
    }

    internal static void Apply(ContentItem item, SaveContentItemRequest request)
    {
        item.Type = request.Type;
        item.Title = request.Title.Trim();
        item.Body = request.Body.Trim();
        item.TargetAudience = string.IsNullOrWhiteSpace(request.TargetAudience) ? null : request.TargetAudience.Trim();
        item.ToneOfVoice = string.IsNullOrWhiteSpace(request.ToneOfVoice) ? null : request.ToneOfVoice.Trim();
        item.Keywords = request.Keywords?
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new();
    }
}
