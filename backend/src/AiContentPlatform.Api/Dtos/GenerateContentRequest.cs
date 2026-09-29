using System.ComponentModel.DataAnnotations;
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
}
