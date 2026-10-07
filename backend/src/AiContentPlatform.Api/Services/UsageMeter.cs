namespace AiContentPlatform.Api.Services;

/// <summary>Collects token counts reported by a provider during one AI call.</summary>
public sealed class UsageMeter
{
    public string? Model { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CacheReadTokens { get; set; }
    public int CacheWriteTokens { get; set; }

    /// <summary>True when counts were estimated rather than reported by the provider.</summary>
    public bool Estimated { get; set; }

    /// <summary>
    /// True once the provider reported the final output count. Streams can report a provisional
    /// count up front (Anthropic's message_start) that is only replaced at the end.
    /// </summary>
    public bool OutputFinal { get; set; }

    /// <summary>Rough token estimate (~4 characters per token) for providers or streams without usage data.</summary>
    public static int EstimateTokens(int characters) => (characters + 3) / 4;
}
