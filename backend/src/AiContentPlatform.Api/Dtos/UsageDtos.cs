using AiContentPlatform.Api.Domain;

namespace AiContentPlatform.Api.Dtos;

public record UsageTotals(int Calls, long InputTokens, long OutputTokens, double CostUsd, int UnpricedCalls, int FailedCalls);

public record UsageByDay(DateOnly Date, int Calls, long Tokens, double CostUsd);

public record UsageByGroup(string Key, string Label, int Calls, long Tokens, double CostUsd);

public record UsageRecordDto(
    Guid Id, DateTime CreatedAt, AiOperation Operation, string? Detail, Guid? ProjectId, string? ProjectName,
    string Provider, string Model, int InputTokens, int OutputTokens, bool Estimated, double? CostUsd, int DurationMs, AiCallStatus Status);

public record UsageReport(
    DateOnly From,
    DateOnly To,
    UsageTotals Totals,
    IReadOnlyList<UsageByDay> ByDay,
    IReadOnlyList<UsageByGroup> ByOperation,
    IReadOnlyList<UsageByGroup> ByProject,
    IReadOnlyList<UsageByGroup> ByModel,
    IReadOnlyList<UsageRecordDto> Recent);
