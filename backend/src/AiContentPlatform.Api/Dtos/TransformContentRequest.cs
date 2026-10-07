using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AiContentPlatform.Api.Domain;
using AiContentPlatform.Api.Services;

namespace AiContentPlatform.Api.Dtos;

public enum TransformAction
{
    Improve,
    Shorten,
    Expand,
    ChangeTone,
    Translate,
    Custom
}

/// <summary>Rewrites existing content with an AI editing action.</summary>
public class TransformContentRequest : IValidatableObject
{
    public TransformAction Action { get; set; }

    [StringLength(300)]
    public string? Title { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Body is required.")]
    [StringLength(50_000)]
    public string Body { get; set; } = string.Empty;

    public ContentType Type { get; set; } = ContentType.Custom;

    /// <summary>Target tone for <see cref="TransformAction.ChangeTone"/>.</summary>
    [StringLength(100)]
    public string? ToneOfVoice { get; set; }

    /// <summary>Target language for <see cref="TransformAction.Translate"/>.</summary>
    [StringLength(50)]
    public string? Language { get; set; }

    /// <summary>Free-form instruction for <see cref="TransformAction.Custom"/>.</summary>
    [StringLength(1000)]
    public string? Instruction { get; set; }

    /// <summary>SEO keywords to preserve; also used to score the result.</summary>
    [MaxLength(10)]
    public string[]? Keywords { get; set; }

    /// <summary>Project whose brand voice should guide the edit.</summary>
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

    /// <summary>Token usage of the main AI call for this request, filled in by the provider.</summary>
    [JsonIgnore]
    public UsageMeter Usage { get; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Action == TransformAction.ChangeTone && string.IsNullOrWhiteSpace(ToneOfVoice))
            yield return new ValidationResult("ToneOfVoice is required to change the tone.", [nameof(ToneOfVoice)]);
        if (Action == TransformAction.Translate && string.IsNullOrWhiteSpace(Language))
            yield return new ValidationResult("Language is required to translate.", [nameof(Language)]);
        if (Action == TransformAction.Custom && string.IsNullOrWhiteSpace(Instruction))
            yield return new ValidationResult("Instruction is required for a custom edit.", [nameof(Instruction)]);
    }
}
