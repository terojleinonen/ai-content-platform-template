using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AiContentPlatform.Api.Data;

/// <summary>SQLite: the default for local development and the demo. Migrations in Data/Migrations/Sqlite.</summary>
public sealed class SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options) : AppDbContext(options);

/// <summary>PostgreSQL: for production. Migrations in Data/Migrations/Postgres.</summary>
public sealed class PostgresAppDbContext(DbContextOptions<PostgresAppDbContext> options) : AppDbContext(options);

// Used by `dotnet ef migrations add --context ...`; generating migrations doesn't connect to a database.
public sealed class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteAppDbContext>
{
    public SqliteAppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqliteAppDbContext>().UseSqlite("Data Source=design-time.db").Options);
}

public sealed class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresAppDbContext>
{
    public PostgresAppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PostgresAppDbContext>().UseNpgsql("Host=localhost;Database=design_time").Options);
}
