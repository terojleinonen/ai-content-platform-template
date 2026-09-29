using System.ComponentModel.DataAnnotations;
using AiContentPlatform.Api.Domain;

namespace AiContentPlatform.Api.Dtos;

public record ContentItemDto(
    Guid Id,
    Guid ProjectId,
    ContentType Type,
    string Title,
    string Body,
    string? TargetAudience,
    string? ToneOfVoice,
    IReadOnlyList<string> Keywords,
    DateTime CreatedAt,
    DateTime? UpdatedAt)
{
    public static ContentItemDto From(ContentItem item) => new(
        item.Id, item.ProjectId, item.Type, item.Title, item.Body,
        item.TargetAudience, item.ToneOfVoice, item.Keywords, item.CreatedAt, item.UpdatedAt);
}

public class SaveContentItemRequest
{
    public ContentType Type { get; set; } = ContentType.BlogPost;

    [Required(AllowEmptyStrings = false)]
    [StringLength(300)]
    public string Title { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(50_000)]
    public string Body { get; set; } = string.Empty;

    [StringLength(200)]
    public string? TargetAudience { get; set; }

    [StringLength(100)]
    public string? ToneOfVoice { get; set; }

    [MaxLength(10)]
    public List<string>? Keywords { get; set; }
}
