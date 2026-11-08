namespace OnlineShop.Infrastructure.Persistence;

public enum DatabaseProvider
{
    PostgreSql,
    SqlServer,
    MySql
}

/// <summary>Database selection (section <c>Database</c>, same section as <c>SeedOptions</c>).</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public const string ConnectionStringName = "Default";

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.PostgreSql;

    /// <summary>Each provider keeps its migrations in its own assembly (schemas differ per provider).</summary>
    public static string MigrationsAssemblyFor(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.PostgreSql => "OnlineShop.Infrastructure.Migrations.PostgreSql",
        _ => throw new NotSupportedException(
            $"Database provider '{provider}' is planned (roadmap phase 12) but not implemented yet. Use PostgreSql.")
    };
}
