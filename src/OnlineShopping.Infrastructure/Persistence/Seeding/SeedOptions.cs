namespace OnlineShopping.Infrastructure.Persistence.Seeding;

/// <summary>
/// Startup database settings (section <c>Database</c>).
/// Keep <see cref="AdminPassword"/> out of source control: use <c>dotnet user-secrets</c> or environment variables.
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "Database";

    /// <summary>Apply pending EF Core migrations when the app starts.</summary>
    public bool MigrateOnStartup { get; set; }

    public string? AdminEmail { get; set; }
    public string? AdminPassword { get; set; }
}
