using System.ComponentModel.DataAnnotations;
using AiContentPlatform.Api.Domain;

namespace AiContentPlatform.Api.Dtos;

public record BrandVoiceDto(
    string? Voice,
    string? TargetAudience,
    string? KeyFacts,
    IReadOnlyList<string> PreferredTerms,
    IReadOnlyList<string> AvoidTerms)
{
    public static BrandVoiceDto? From(BrandVoice? brand) => brand is null || brand.IsEmpty
        ? null
        : new(brand.Voice, brand.TargetAudience, brand.KeyFacts, brand.PreferredTerms, brand.AvoidTerms);
}

public class SaveBrandVoiceRequest
{
    [StringLength(2000)]
    public string? Voice { get; set; }

    [StringLength(500)]
    public string? TargetAudience { get; set; }

    [StringLength(4000)]
    public string? KeyFacts { get; set; }

    [MaxLength(20)]
    public List<string>? PreferredTerms { get; set; }

    [MaxLength(20)]
    public List<string>? AvoidTerms { get; set; }
}

/// <summary>How well generated content follows the project's brand voice.</summary>
public record BrandCheckResult(
    string ProjectName,
    IReadOnlyList<string> AvoidTermsFound,
    IReadOnlyList<string> PreferredTermsUsed,
    IReadOnlyList<string> PreferredTermsMissing);
