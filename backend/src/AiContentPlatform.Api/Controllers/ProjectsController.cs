using AiContentPlatform.Api.Data;
using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiContentPlatform.Api.Controllers;

// TODO: add authentication and scope projects to the signed-in user.
// Until then every project belongs to the seeded demo user.
[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProjectsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IReadOnlyList<ProjectDto>> List(CancellationToken cancellationToken)
    {
        return await QueryProjects(_db.Projects.OrderByDescending(p => p.CreatedAt), cancellationToken);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var project = (await QueryProjects(_db.Projects.Where(p => p.Id == id), cancellationToken)).FirstOrDefault();
        return project is null ? NotFound() : project;
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create([FromBody] SaveProjectRequest request, CancellationToken cancellationToken)
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            OwnerId = SeedData.DemoUserId
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = new ProjectDto(project.Id, project.Name, project.Description, project.CreatedAt, 0, null);
        return CreatedAtAction(nameof(Get), new { id = project.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> Update(Guid id, [FromBody] SaveProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await _db.Projects.Include(p => p.ContentItems).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null) return NotFound();

        project.Name = request.Name.Trim();
        project.Description = request.Description?.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(project);
    }

    /// <summary>Sets the project's brand voice. Sending only empty fields removes it.</summary>
    [HttpPut("{id:guid}/brand-voice")]
    public async Task<ActionResult<ProjectDto>> SaveBrandVoice(Guid id, [FromBody] SaveBrandVoiceRequest request, CancellationToken cancellationToken)
    {
        var project = await _db.Projects.Include(p => p.ContentItems).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (project is null) return NotFound();

        var brand = new BrandVoice
        {
            Voice = Clean(request.Voice),
            TargetAudience = Clean(request.TargetAudience),
            KeyFacts = Clean(request.KeyFacts),
            PreferredTerms = CleanTerms(request.PreferredTerms),
            AvoidTerms = CleanTerms(request.AvoidTerms)
        };
        project.BrandVoice = brand.IsEmpty ? null : brand;
        await _db.SaveChangesAsync(cancellationToken);

        return ToDto(project);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _db.Projects.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? NotFound() : NoContent();
    }

    private static ProjectDto ToDto(Project p) =>
        new(p.Id, p.Name, p.Description, p.CreatedAt, p.ContentItems.Count, BrandVoiceDto.From(p.BrandVoice));

    // Owned JSON entities can only be projected from no-tracking queries.
    private static async Task<List<ProjectDto>> QueryProjects(IQueryable<Project> query, CancellationToken cancellationToken)
    {
        var rows = await query
            .AsNoTracking()
            .Select(p => new { p.Id, p.Name, p.Description, p.CreatedAt, Count = p.ContentItems.Count, p.BrandVoice })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new ProjectDto(r.Id, r.Name, r.Description, r.CreatedAt, r.Count, BrandVoiceDto.From(r.BrandVoice))).ToList();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> CleanTerms(IEnumerable<string>? terms) =>
        terms?.Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new();

    [HttpGet("{id:guid}/content")]
    public async Task<ActionResult<IReadOnlyList<ContentItemDto>>> ListContent(Guid id, CancellationToken cancellationToken)
    {
        if (!await _db.Projects.AnyAsync(p => p.Id == id, cancellationToken)) return NotFound();

        var items = await _db.ContentItems
            .Where(c => c.ProjectId == id)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .ToListAsync(cancellationToken);
        return items.Select(ContentItemDto.From).ToList();
    }

    [HttpPost("{id:guid}/content")]
    public async Task<ActionResult<ContentItemDto>> AddContent(Guid id, [FromBody] SaveContentItemRequest request, CancellationToken cancellationToken)
    {
        if (!await _db.Projects.AnyAsync(p => p.Id == id, cancellationToken)) return NotFound();

        var item = new ContentItem { Id = Guid.NewGuid(), ProjectId = id };
        ContentItemsController.Apply(item, request);

        _db.ContentItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(ContentItemsController.Get), "ContentItems", new { id = item.Id }, ContentItemDto.From(item));
    }
}
