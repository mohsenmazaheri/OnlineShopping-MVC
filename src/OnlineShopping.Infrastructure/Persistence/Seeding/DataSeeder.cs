using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineShopping.Application.Authorization;
using OnlineShopping.Infrastructure.Identity;

namespace OnlineShopping.Infrastructure.Persistence.Seeding;

/// <summary>
/// Prepares the database at startup: applies migrations (when enabled), creates the roles and,
/// when credentials are configured, the first admin account. Safe to run on every start.
/// </summary>
public sealed partial class DataSeeder(
    AppDbContext db,
    RoleManager<IdentityRole> roleManager,
    UserManager<AppUser> userManager,
    IOptions<SeedOptions> options,
    ILogger<DataSeeder> logger,
    TimeProvider clock)
{

    private readonly SeedOptions _options = options.Value;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (_options.MigrateOnStartup)
            await db.Database.MigrateAsync(cancellationToken);

        await EnsureRolesAsync();
        await EnsureAdminAsync();
    }

    private async Task EnsureRolesAsync()
    {
        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole(role)), $"create role '{role}'");
        }
    }

    private async Task EnsureAdminAsync()
    {
        if (string.IsNullOrWhiteSpace(_options.AdminEmail) || string.IsNullOrWhiteSpace(_options.AdminPassword))
        {
            LogAdminNotConfigured(logger, SeedOptions.SectionName);
            return;
        }

        var admin = await userManager.FindByEmailAsync(_options.AdminEmail);
        if (admin is null)
        {
            admin = new AppUser
            {
                UserName = _options.AdminEmail,
                Email = _options.AdminEmail,
                EmailConfirmed = true,
                DisplayName = "Admin",
                CreatedUtc = clock.GetUtcNow().UtcDateTime
            };
            EnsureSucceeded(await userManager.CreateAsync(admin, _options.AdminPassword), "create the admin user");
            LogAdminCreated(logger, _options.AdminEmail);
        }

        if (!await userManager.IsInRoleAsync(admin, AppRoles.Admin))
            EnsureSucceeded(await userManager.AddToRoleAsync(admin, AppRoles.Admin), "add the admin user to the Admin role");
    }

    /// <summary>Identity reports failures through results, not exceptions; ignoring them hides broken seeding.</summary>
    private static void EnsureSucceeded(IdentityResult result, string action)
    {
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            throw new InvalidOperationException($"Database seeding failed to {action}. {errors}");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "No admin account seeded: set {Section}:AdminEmail and {Section}:AdminPassword (e.g. with dotnet user-secrets).")]
    private static partial void LogAdminNotConfigured(ILogger logger, string section);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin account {Email} created.")]
    private static partial void LogAdminCreated(ILogger logger, string email);
}
