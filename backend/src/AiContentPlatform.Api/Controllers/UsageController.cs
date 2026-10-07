using System.ComponentModel.DataAnnotations;
using AiContentPlatform.Api.Auth;
using AiContentPlatform.Api.Data;
using AiContentPlatform.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiContentPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsageController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _user;

    public UsageController(AppDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    /// <summary>The signed-in user's AI usage and estimated cost for the last <paramref name="days"/> days (UTC).</summary>
    [HttpGet]
    public async Task<UsageReport> Get([FromQuery, Range(1, 365)] int days = 30, CancellationToken cancellationToken = default)
    {
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = to.AddDays(-(days - 1));
        var since = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Aggregated in memory: portable across SQLite/PostgreSQL, and fine at this app's volume.
        var records = await _db.AiUsage
            .AsNoTracking()
            .Where(r => r.UserId == _user.RequiredId && r.CreatedAt >= since)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var projectNames = await _db.Projects
            .AsNoTracking()
            .Where(p => p.OwnerId == _user.RequiredId)
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);
        string ProjectName(Guid? id) =>
            id is null ? "No project" : projectNames.GetValueOrDefault(id.Value, "Deleted project");

        UsageByGroup Group(string key, string label, IEnumerable<Domain.AiUsageRecord> rows) =>
            new(key, label, rows.Count(), rows.Sum(r => (long)r.InputTokens + r.OutputTokens), rows.Sum(r => r.CostUsd ?? 0));

        var byDay = Enumerable.Range(0, days)
            .Select(i => from.AddDays(i))
            .Select(day =>
            {
                var rows = records.Where(r => DateOnly.FromDateTime(r.CreatedAt) == day).ToList();
                return new UsageByDay(day, rows.Count, rows.Sum(r => (long)r.InputTokens + r.OutputTokens), rows.Sum(r => r.CostUsd ?? 0));
            })
            .ToList();

        return new UsageReport(
            from,
            to,
            new UsageTotals(
                records.Count,
                records.Sum(r => (long)r.InputTokens),
                records.Sum(r => (long)r.OutputTokens),
                records.Sum(r => r.CostUsd ?? 0),
                records.Count(r => r.CostUsd is null),
                records.Count(r => r.Status == Domain.AiCallStatus.Failed)),
            byDay,
            records.GroupBy(r => r.Operation).Select(g => Group(g.Key.ToString(), g.Key.ToString(), g)).OrderByDescending(g => g.CostUsd).ThenByDescending(g => g.Calls).ToList(),
            records.GroupBy(r => r.ProjectId).Select(g => Group(g.Key?.ToString() ?? "", ProjectName(g.Key), g)).OrderByDescending(g => g.CostUsd).ThenByDescending(g => g.Calls).ToList(),
            records.GroupBy(r => r.Model).Select(g => Group(g.Key, g.Key, g)).OrderByDescending(g => g.CostUsd).ThenByDescending(g => g.Calls).ToList(),
            records.Take(25).Select(r => new UsageRecordDto(
                r.Id, r.CreatedAt, r.Operation, r.Detail, r.ProjectId, r.ProjectId is null ? null : ProjectName(r.ProjectId),
                r.Provider, r.Model, r.InputTokens, r.OutputTokens, r.Estimated, r.CostUsd, r.DurationMs, r.Status)).ToList());
    }
}
