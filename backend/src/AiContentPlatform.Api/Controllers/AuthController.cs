using AiContentPlatform.Api.Auth;
using AiContentPlatform.Api.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AiContentPlatform.Api.Controllers;

public record CurrentUserDto(Guid Id, string Email, string DisplayName, bool HasPassword, IReadOnlyList<string> ExternalLogins);

/// <summary>
/// Account endpoints next to Identity's built-in ones (POST /api/auth/register, /api/auth/login, ...).
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<User> _users;
    private readonly SignInManager<User> _signIn;
    private readonly ExternalLoginService _externalLogins;
    private readonly IAuthenticationSchemeProvider _schemes;

    public AuthController(UserManager<User> users, SignInManager<User> signIn, ExternalLoginService externalLogins, IAuthenticationSchemeProvider schemes)
    {
        _users = users;
        _signIn = signIn;
        _externalLogins = externalLogins;
        _schemes = schemes;
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserDto>> Me()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var logins = await _users.GetLoginsAsync(user);
        return new CurrentUserDto(
            user.Id,
            user.Email ?? string.Empty,
            string.IsNullOrWhiteSpace(user.DisplayName) ? (user.Email ?? string.Empty).Split('@')[0] : user.DisplayName,
            await _users.HasPasswordAsync(user),
            logins.Select(l => l.ProviderDisplayName ?? l.LoginProvider).ToList());
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return NoContent();
    }

    /// <summary>External sign-in providers that are configured (shown as buttons on the login page).</summary>
    [AllowAnonymous]
    [HttpGet("providers")]
    public async Task<IReadOnlyList<string>> Providers() =>
        (await _signIn.GetExternalAuthenticationSchemesAsync()).Select(s => s.Name).ToList();

    /// <summary>Starts a Google/Microsoft sign-in: redirects to the provider.</summary>
    [AllowAnonymous]
    [HttpGet("external/{provider}")]
    public async Task<IActionResult> External(string provider, [FromQuery] string? returnUrl = "/")
    {
        var scheme = (await _signIn.GetExternalAuthenticationSchemesAsync())
            .FirstOrDefault(s => string.Equals(s.Name, provider, StringComparison.OrdinalIgnoreCase));
        if (scheme is null) return NotFound();

        var callback = Url.Action(nameof(ExternalCallback), new { returnUrl = SafeReturnUrl(returnUrl) });
        var properties = _signIn.ConfigureExternalAuthenticationProperties(scheme.Name, callback);
        return Challenge(properties, scheme.Name);
    }

    /// <summary>Where the provider sends the user back: signs in (creating the account if new).</summary>
    [AllowAnonymous]
    [HttpGet("external/callback")]
    public async Task<IActionResult> ExternalCallback([FromQuery] string? returnUrl = "/")
    {
        returnUrl = SafeReturnUrl(returnUrl);
        var info = await _signIn.GetExternalLoginInfoAsync();
        if (info is null) return Redirect(WithError("External sign-in failed or was cancelled."));

        var result = await _externalLogins.ResolveUserAsync(info);
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        if (result.User is null) return Redirect(WithError(result.Error!));

        await _signIn.SignInAsync(result.User, isPersistent: true, info.LoginProvider);
        return Redirect(returnUrl);
    }

    // Only local paths, so the sign-in flow can't be used as an open redirect.
    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/";

    // Back to the app's sign-in page, which shows the message.
    private static string WithError(string error) => $"/?authError={Uri.EscapeDataString(error)}";
}
