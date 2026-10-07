using AiContentPlatform.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiContentPlatform.Api.Data;

/// <summary>
/// Applies EF Core migrations and seeds a demo user/project so the app is usable on first run.
/// Add schema changes with `dotnet ef migrations add &lt;Name&gt; --output-dir Data/Migrations`.
/// </summary>
public static class SeedData
{
    public static readonly Guid DemoUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    // Databases created before migrations were introduced (via EnsureCreated) match this migration.
    private const string BaselineMigrationId = "20261007182527_InitialCreate";

    public static async Task InitializeAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        await BaselineLegacyDatabaseAsync(db, cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);

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
            OwnerId = user.Id,
            BrandVoice = new BrandVoice
            {
                Voice = "Warm, knowledgeable and a little playful, like a friendly barista. Short sentences. Confident, never hypey.",
                TargetAudience = "Home coffee enthusiasts in Finland",
                KeyFacts = """
                    Single-origin beans from three partner farms in Ethiopia, Colombia and Guatemala
                    Roasted to order in Helsinki and shipped within 48 hours
                    Subscription from €14.90 per month, cancel anytime
                    First bag free for new subscribers
                    """,
                PreferredTerms = ["freshly roasted", "single-origin"],
                AvoidTerms = ["cheap", "best coffee in the world", "revolutionary"]
            }
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

    /// <summary>
    /// Marks a database created by the old EnsureCreated startup as already having the baseline
    /// migration, so MigrateAsync only applies the newer ones instead of failing on existing tables.
    /// </summary>
    private static async Task BaselineLegacyDatabaseAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlite()) return;

        var hasTables = await TableExistsAsync(db, "Projects", cancellationToken);
        var hasHistory = await TableExistsAsync(db, "__EFMigrationsHistory", cancellationToken);
        if (!hasTables || hasHistory) return;

        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL);
            """, cancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO \"__EFMigrationsHistory\" VALUES ({BaselineMigrationId}, '10.0.12')", cancellationToken);
    }

    private static async Task<bool> TableExistsAsync(AppDbContext db, string table, CancellationToken cancellationToken) =>
        await db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM sqlite_master WHERE type = 'table' AND name = {table}")
            .SingleAsync(cancellationToken) > 0;
}
