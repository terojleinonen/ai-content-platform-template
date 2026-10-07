using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Services;

namespace AiContentPlatform.Api.Dtos;

public class GenerateContentRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Prompt is required.")]
    [StringLength(4000)]
    public string Prompt { get; set; } = string.Empty;

    public ContentType Type { get; set; } = ContentType.BlogPost;

    [StringLength(300)]
    public string? Title { get; set; }

    [StringLength(200)]
    public string? TargetAudience { get; set; }

    [StringLength(100)]
    public string? ToneOfVoice { get; set; }

    [StringLength(50)]
    public string? Language { get; set; }

    [MaxLength(10)]
    public string[]? Keywords { get; set; }

    /// <summary>Project whose brand voice should guide the writing.</summary>
    public Guid? ProjectId { get; set; }

    /// <summary>Brand voice resolved from <see cref="ProjectId"/> by the server.</summary>
    [JsonIgnore]
    public BrandContext? Brand { get; set; }

    /// <summary>
    /// Keywords and brand terms translated into the content's language (original → translated),
    /// set by the server before writing; null when no translation was needed.
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, string>? TermTranslations { get; set; }

    [JsonIgnore]
    public bool TermsLocalized { get; set; }

    /// <summary>
    /// 1-based variant number when generating several alternatives; 0 for a single generation.
    /// Set by the server, not by clients.
    /// </summary>
    [JsonIgnore]
    public int Variant { get; set; }

    public GenerateContentRequest AsVariant(int variant) => new()
    {
        Prompt = Prompt,
        Type = Type,
        Title = Title,
        TargetAudience = TargetAudience,
        ToneOfVoice = ToneOfVoice,
        Language = Language,
        Keywords = Keywords,
        ProjectId = ProjectId,
        Brand = Brand,
        TermTranslations = TermTranslations,
        TermsLocalized = TermsLocalized,
        Variant = variant
    };
}
