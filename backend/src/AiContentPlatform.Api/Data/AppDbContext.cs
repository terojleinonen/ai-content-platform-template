using AiContentPlatform.Api.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AiContentPlatform.Api.Data;

/// <summary>
/// The app's database. Abstract because each database engine has its own concrete context (and
/// migrations): <see cref="SqliteAppDbContext"/> and <see cref="PostgresAppDbContext"/>. Code
/// depends on this base type; <see cref="DatabaseSetup"/> registers the configured one.
/// </summary>
public abstract class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    protected AppDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ContentItem> ContentItems => Set<ContentItem>();
    public DbSet<AiUsageRecord> AiUsage => Set<AiUsageRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(e =>
        {
            // Identity's user table, kept under the name used before accounts existed.
            e.ToTable("Users");
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.DisplayName).HasMaxLength(128);
        });

        modelBuilder.Entity<Project>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200);
            e.OwnsOne(p => p.BrandVoice, b => b.ToJson());
            e.HasOne(p => p.Owner)
                .WithMany(u => u.Projects)
                .HasForeignKey(p => p.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AiUsageRecord>(e =>
        {
            e.ToTable("AiUsage");
            e.HasIndex(u => u.CreatedAt);
            e.HasIndex(u => u.ProjectId);
            e.HasIndex(u => u.UserId);
            e.Property(u => u.Operation).HasConversion<string>().HasMaxLength(32);
            e.Property(u => u.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(u => u.Detail).HasMaxLength(64);
            e.Property(u => u.Provider).HasMaxLength(32);
            e.Property(u => u.Model).HasMaxLength(100);
            // No FK: usage history is kept (for billing) even when a project is deleted.
        });

        modelBuilder.Entity<ContentItem>(e =>
        {
            e.Property(c => c.Title).HasMaxLength(300);
            e.Property(c => c.Type).HasConversion<string>().HasMaxLength(32);
            e.HasOne(c => c.Project)
                .WithMany(p => p.ContentItems)
                .HasForeignKey(c => c.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
