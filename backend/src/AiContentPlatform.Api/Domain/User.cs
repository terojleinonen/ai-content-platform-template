using Microsoft.AspNetCore.Identity;

namespace AiContentPlatform.Api.Domain;

/// <summary>
/// An account. Password hashing, lockout, external logins etc. come from ASP.NET Core Identity.
/// </summary>
public class User : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Project> Projects { get; set; } = new();
}
