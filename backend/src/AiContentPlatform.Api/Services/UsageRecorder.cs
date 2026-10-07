using System.Diagnostics;
using AiContentPlatform.Api.Data;
using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiContentPlatform.Api.Services;

public record UsageContext(AiOperation Operation, string? Detail = null, Guid? ProjectId = null);

public interface IUsageRecorder
{
    /// <summary>Estimated cost in USD for the metered tokens; null when the model has no configured price.</summary>
    double? PriceOf(UsageMeter meter);

    /// <summary>Stores one call. Never throws: usage tracking must not break generation.</summary>
    Task RecordAsync(UsageContext context, string provider, UsageMeter meter, TimeSpan duration, AiCallStatus status);
}

public class UsageRecorder : IUsageRecorder
{
    // Prompt-cache pricing relative to the input price.
    private const double CacheWriteMultiplier = 1.25;
    private const double CacheReadMultiplier = 0.1;

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IOptionsMonitor<AiOptions> _options;
    private readonly ILogger<UsageRecorder> _logger;

    public UsageRecorder(IDbContextFactory<AppDbContext> dbFactory, IOptionsMonitor<AiOptions> options, ILogger<UsageRecorder> logger)
    {
        _dbFactory = dbFactory;
        _options = options;
        _logger = logger;
    }

    public double? PriceOf(UsageMeter meter)
    {
        if (meter.Model is null) return null;
        if (meter.Model == MockModel) return 0;
        if (!_options.CurrentValue.Pricing.TryGetValue(meter.Model, out var price)) return null;

        var cost = (meter.InputTokens * price.InputPerMTok
                    + meter.CacheWriteTokens * price.InputPerMTok * CacheWriteMultiplier
                    + meter.CacheReadTokens * price.InputPerMTok * CacheReadMultiplier
                    + meter.OutputTokens * price.OutputPerMTok) / 1_000_000;
        return Math.Round(cost, 6);
    }

    public const string MockModel = "mock";

    public async Task RecordAsync(UsageContext context, string provider, UsageMeter meter, TimeSpan duration, AiCallStatus status)
    {
        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            db.AiUsage.Add(new AiUsageRecord
            {
                Id = Guid.NewGuid(),
                Operation = context.Operation,
                Detail = context.Detail,
                ProjectId = context.ProjectId,
                Provider = provider,
                Model = meter.Model ?? "unknown",
                InputTokens = meter.InputTokens,
                OutputTokens = meter.OutputTokens,
                CacheReadTokens = meter.CacheReadTokens,
                CacheWriteTokens = meter.CacheWriteTokens,
                Estimated = meter.Estimated,
                CostUsd = PriceOf(meter),
                DurationMs = (int)duration.TotalMilliseconds,
                Status = status
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record AI usage for {Operation}", context.Operation);
        }
    }
}

/// <summary>Times an AI call and records its outcome, whether it succeeds, fails or is cancelled.</summary>
public static class UsageRecorderExtensions
{
    public static async Task<T> TrackAsync<T>(this IUsageRecorder recorder, UsageContext context, string provider, UsageMeter meter, Func<Task<T>> call)
    {
        var stopwatch = Stopwatch.StartNew();
        var status = AiCallStatus.Failed;
        try
        {
            var result = await call();
            status = AiCallStatus.Succeeded;
            return result;
        }
        catch (OperationCanceledException)
        {
            status = AiCallStatus.Cancelled;
            throw;
        }
        finally
        {
            await recorder.RecordAsync(context, provider, meter, stopwatch.Elapsed, status);
        }
    }
}
