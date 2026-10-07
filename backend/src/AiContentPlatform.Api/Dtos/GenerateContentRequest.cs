using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using AiContentPlatform.Api.Domain;

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
        Variant = variant
    };
}
