namespace AiContentPlatform.Api.Domain;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    // In a real project, do NOT store passwords like this.
    // Use ASP.NET Core Identity or an external provider.
    public string PasswordHash { get; set; } = string.Empty;

    public List<Project> Projects { get; set; } = new();
}
