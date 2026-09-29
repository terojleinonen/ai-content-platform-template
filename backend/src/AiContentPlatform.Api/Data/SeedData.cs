using AiContentPlatform.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiContentPlatform.Api.Data;

/// <summary>
/// Creates the database and a demo user/project so the app is usable on first run.
/// For a real deployment, switch to EF Core migrations (`dotnet ef migrations add ...`).
/// </summary>
public static class SeedData
{
    public static readonly Guid DemoUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static async Task InitializeAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var user = new User
        {
            Id = DemoUserId,
            Email = "demo@example.com",
            DisplayName = "Demo User"
        };

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Acme Coffee Launch",
            Description = "Launch campaign for Acme's new single-origin coffee subscription.",
            OwnerId = user.Id
        };

        project.ContentItems.Add(new ContentItem
        {
            Id = Guid.NewGuid(),
            Type = ContentType.BlogPost,
            Title = "Why Single-Origin Coffee Tastes Better",
            Body = """
                Single-origin coffee comes from one farm or region, which means every cup tells the story of a specific place.

                ## Flavor you can trace
                Because beans are not blended, the natural character of the soil, altitude and climate shines through.

                ## Fresh to your door
                Our subscription roasts to order and ships within 48 hours, so you always brew at peak freshness.
                """,
            TargetAudience = "Home coffee enthusiasts",
            ToneOfVoice = "Friendly",
            Keywords = ["single-origin coffee", "coffee subscription"]
        });

        project.ContentItems.Add(new ContentItem
        {
            Id = Guid.NewGuid(),
            Type = ContentType.SocialPost,
            Title = "Launch announcement",
            Body = "☕ It's here! Fresh single-origin coffee, roasted to order and delivered to your door. First bag is on us — link in bio. #coffee #subscription",
            ToneOfVoice = "Playful",
            Keywords = ["coffee"]
        });

        db.Users.Add(user);
        db.Projects.Add(project);
        await db.SaveChangesAsync(cancellationToken);
    }
}
