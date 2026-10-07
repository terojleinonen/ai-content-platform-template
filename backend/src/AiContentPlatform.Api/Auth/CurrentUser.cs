using System.Security.Claims;

namespace AiContentPlatform.Api.Auth;

public interface ICurrentUser
{
    /// <summary>The signed-in user's id, or null outside an authenticated request.</summary>
    Guid? Id { get; }

    /// <summary>The signed-in user's id; for code that only runs behind [Authorize].</summary>
    Guid RequiredId => Id ?? throw new InvalidOperationException("No signed-in user.");
}

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUser(IHttpContextAccessor http)
    {
        _http = http;
    }

    public Guid? Id =>
        Guid.TryParse(_http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
