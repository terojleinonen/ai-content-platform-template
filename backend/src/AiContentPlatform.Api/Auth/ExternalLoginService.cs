using System.Security.Claims;
using AiContentPlatform.Api.Domain;
using Microsoft.AspNetCore.Identity;

namespace AiContentPlatform.Api.Auth;

public record ExternalLoginResult(User? User, string? Error);

/// <summary>Finds or creates the account for a Google/Microsoft sign-in.</summary>
public class ExternalLoginService
{
    private readonly UserManager<User> _users;

    public ExternalLoginService(UserManager<User> users)
    {
        _users = users;
    }

    public async Task<ExternalLoginResult> ResolveUserAsync(ExternalLoginInfo info)
    {
        // Returning user: this provider account is already linked.
        var user = await _users.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (user is not null) return new(user, null);

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return new(null, $"{info.ProviderDisplayName} didn't share an email address.");
        }

        // Deliberately no automatic linking to an existing account with the same email: not every
        // provider guarantees a verified address, and linking on email alone allows account takeover.
        if (await _users.FindByEmailAsync(email) is not null)
        {
            return new(null, "An account with this email already exists. Sign in with your password.");
        }

        user = new User
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email
        };
        var created = await _users.CreateAsync(user);
        if (created.Succeeded) created = await _users.AddLoginAsync(user, info);
        return created.Succeeded
            ? new(user, null)
            : new(null, string.Join(" ", created.Errors.Select(e => e.Description)));
    }
}
