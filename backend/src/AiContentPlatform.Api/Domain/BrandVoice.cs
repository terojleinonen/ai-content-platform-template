namespace AiContentPlatform.Api.Domain;

/// <summary>
/// Brand guidelines for a project, added to every AI prompt for content in that project.
/// </summary>
public class BrandVoice
{
    /// <summary>Personality and style, e.g. "Warm and witty, short sentences, no hype".</summary>
    public string? Voice { get; set; }

    public string? TargetAudience { get; set; }

    /// <summary>Facts the AI may rely on (products, prices, claims), one per line.</summary>
    public string? KeyFacts { get; set; }

    public List<string> PreferredTerms { get; set; } = new();

    public List<string> AvoidTerms { get; set; } = new();

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Voice) &&
        string.IsNullOrWhiteSpace(TargetAudience) &&
        string.IsNullOrWhiteSpace(KeyFacts) &&
        PreferredTerms.Count == 0 &&
        AvoidTerms.Count == 0;
}
