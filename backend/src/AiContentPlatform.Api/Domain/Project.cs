namespace AiContentPlatform.Api.Domain;

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public List<ContentItem> ContentItems { get; set; } = new();
}
