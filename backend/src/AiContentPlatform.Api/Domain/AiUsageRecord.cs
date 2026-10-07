namespace AiContentPlatform.Api.Domain;

public enum AiOperation
{
    Generate,
    Variant,
    Transform,
    TermTranslation,
    Image
}

public enum AiCallStatus
{
    Succeeded,
    Failed,
    Cancelled
}

/// <summary>One AI provider call: tokens, estimated cost and outcome.</summary>
public class AiUsageRecord
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public AiOperation Operation { get; set; }

    /// <summary>Extra detail, e.g. the editing action for <see cref="AiOperation.Transform"/>.</summary>
    public string? Detail { get; set; }

    public Guid? ProjectId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CacheReadTokens { get; set; }
    public int CacheWriteTokens { get; set; }

    /// <summary>True when token counts were estimated from text length (mock provider, interrupted streams).</summary>
    public bool Estimated { get; set; }

    /// <summary>Estimated cost in USD; null when the model has no configured price.</summary>
    public double? CostUsd { get; set; }

    public int DurationMs { get; set; }
    public AiCallStatus Status { get; set; }
}
