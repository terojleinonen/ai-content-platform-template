using System.ComponentModel.DataAnnotations;

namespace AiContentPlatform.Api.Dtos;

public record ProjectDto(Guid Id, string Name, string? Description, DateTime CreatedAt, int ContentCount);

public class SaveProjectRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }
}
