using AiContentPlatform.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiContentPlatform.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ContentItem> ContentItems => Set<ContentItem>();
    public DbSet<AiUsageRecord> AiUsage => Set<AiUsageRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256);
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
