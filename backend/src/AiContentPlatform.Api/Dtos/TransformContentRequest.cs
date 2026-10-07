using System.ComponentModel.DataAnnotations;
using AiContentPlatform.Api.Domain;

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
