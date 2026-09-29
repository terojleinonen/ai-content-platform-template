using System.ComponentModel.DataAnnotations;

namespace AiContentPlatform.Api.Dtos;

public class GenerateImageRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Prompt is required.")]
    [StringLength(4000)]
    public string Prompt { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Style { get; set; }

    [Range(256, 2048)]
    public int Width { get; set; } = 1024;

    [Range(256, 2048)]
    public int Height { get; set; } = 1024;
}
