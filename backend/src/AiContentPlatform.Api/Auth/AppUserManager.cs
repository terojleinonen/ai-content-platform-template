using AiContentPlatform.Api.Data;
using AiContentPlatform.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AiContentPlatform.Api.Auth;

/// <summary>
/// Identity's user manager, plus: the first account ever created takes over the data that
/// belonged to the built-in demo user (the seeded demo project, and anything created before
/// accounts existed), so upgrading an existing install keeps its content.
/// </summary>
public class AppUserManager : UserManager<User>
{
    private readonly AppDbContext _db;

    public AppUserManager(
        IUserStore<User> store,
        IOptions<IdentityOptions> optionsAccessor,
        IPasswordHasher<User> passwordHasher,
        IEnumerable<IUserValidator<User>> userValidators,
        IEnumerable<IPasswordValidator<User>> passwordValidators,
        ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors,
        IServiceProvider services,
        ILogger<UserManager<User>> logger,
        AppDbContext db)
        : base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors, services, logger)
    {
        _db = db;
    }

    public override async Task<IdentityResult> CreateAsync(User user)
    {
        var result = await base.CreateAsync(user);
        if (result.Succeeded)
        {
            await ClaimDemoDataAsync(user);
        }
        return result;
    }

    private async Task ClaimDemoDataAsync(User user)
    {
        var moved = await _db.Projects
            .Where(p => p.OwnerId == SeedData.DemoUserId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.OwnerId, user.Id));
        if (moved == 0) return;

        await _db.AiUsage
            .Where(u => u.UserId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.UserId, user.Id));
        Logger.LogInformation("Account {Email} took over {Count} demo project(s)", user.Email, moved);
    }
}
