using Microsoft.EntityFrameworkCore;

namespace AiContentPlatform.Api.Data;

public static class DatabaseSetup
{
    /// <summary>
    /// Registers the database selected by <c>Database:Provider</c> ("Sqlite" or "Postgres") with
    /// the connection string <c>ConnectionStrings:Default</c>, exposed as <see cref="AppDbContext"/>
    /// (scoped) and <c>IDbContextFactory&lt;AppDbContext&gt;</c>.
    /// </summary>
    public static IServiceCollection AddAppDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        var provider = configuration["Database:Provider"] ?? "Sqlite";
        switch (provider.Trim().ToLowerInvariant())
        {
            case "sqlite":
                services.AddDbContextFactory<SqliteAppDbContext>(o => o.UseSqlite(connectionString));
                Expose<SqliteAppDbContext>(services);
                break;
            case "postgres" or "postgresql" or "npgsql":
                services.AddDbContextFactory<PostgresAppDbContext>(o => o.UseNpgsql(connectionString));
                Expose<PostgresAppDbContext>(services);
                break;
            default:
                throw new InvalidOperationException($"Unknown Database:Provider '{provider}'. Use \"Sqlite\" or \"Postgres\".");
        }

        return services;
    }

    private static void Expose<TContext>(IServiceCollection services) where TContext : AppDbContext
    {
        services.AddScoped<AppDbContext>(sp => sp.GetRequiredService<TContext>());
        services.AddSingleton<IDbContextFactory<AppDbContext>>(sp =>
            new FactoryAdapter<TContext>(sp.GetRequiredService<IDbContextFactory<TContext>>()));
    }

    private sealed class FactoryAdapter<TContext>(IDbContextFactory<TContext> inner) : IDbContextFactory<AppDbContext>
        where TContext : AppDbContext
    {
        public AppDbContext CreateDbContext() => inner.CreateDbContext();
    }
}
