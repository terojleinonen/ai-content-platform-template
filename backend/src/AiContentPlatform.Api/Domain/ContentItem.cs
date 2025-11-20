namespace AiContentPlatform.Api.Domain;

public enum ContentType
{
    BlogPost,
    ProductDescription,
    SocialPost,
    Email,
    Custom
}

public class ContentItem
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public ContentType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? TargetAudience { get; set; }
    public string? ToneOfVoice { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
