using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OnlineShopping.Infrastructure.Persistence;
using OnlineShopping.Infrastructure.Persistence.Seeding;
using OnlineShopping.Infrastructure.Persistence.Interceptors;
using OnlineShopping.Application.Authorization;
using OnlineShopping.Infrastructure.Identity;
using OnlineShopping.UnitTests.TestSupport;

namespace OnlineShopping.UnitTests.Infrastructure.Persistence;

public sealed class DataSeederTests : IDisposable
{
    private const string AdminEmail = "admin@test.local";
    private const string ValidPassword = "Test-Only-Pa55word";

    private readonly SqliteTestDatabase _database = new();
    private readonly ServiceProvider _services;

    public DataSeederTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o
            .UseSqlite(_database.Connection)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditableEntityInterceptor(_database.Clock)));
        services.AddIdentityCore<AppUser>(o =>
            {
                o.Password.RequireDigit = true;
                o.Password.RequiredLength = 6;
                o.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>();
        _services = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        _database.Dispose();
    }

    [Fact]
    public async Task Creates_every_application_role()
    {
        await SeedAsync(new SeedOptions());

        using var scope = _services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in AppRoles.All)
            Assert.True(await roles.RoleExistsAsync(role), $"Role {role} missing");
    }

    [Fact]
    public async Task Running_twice_does_not_duplicate_roles_or_admin()
    {
        var options = new SeedOptions { AdminEmail = AdminEmail, AdminPassword = ValidPassword };

        await SeedAsync(options);
        await SeedAsync(options);

        await using var db = _database.CreateContext();
        Assert.Equal(AppRoles.All.Count, await db.Roles.CountAsync());
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Skips_the_admin_when_no_credentials_are_configured()
    {
        await SeedAsync(new SeedOptions { AdminEmail = AdminEmail });

        await using var db = _database.CreateContext();
        Assert.Equal(0, await db.Users.CountAsync());
    }

    [Fact]
    public async Task Creates_a_confirmed_admin_in_the_Admin_role()
    {
        await SeedAsync(new SeedOptions { AdminEmail = AdminEmail, AdminPassword = ValidPassword });

        using var scope = _services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var admin = await users.FindByEmailAsync(AdminEmail);
        Assert.NotNull(admin);
        Assert.True(admin.EmailConfirmed);
        Assert.True(await users.IsInRoleAsync(admin, AppRoles.Admin));
        Assert.True(await users.CheckPasswordAsync(admin, ValidPassword));
    }

    [Fact]
    public async Task Adds_an_existing_user_to_the_Admin_role()
    {
        using (var scope = _services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            await users.CreateAsync(new AppUser { UserName = AdminEmail, Email = AdminEmail }, ValidPassword);
        }

        await SeedAsync(new SeedOptions { AdminEmail = AdminEmail, AdminPassword = ValidPassword });

        using var verify = _services.CreateScope();
        var userManager = verify.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        Assert.True(await userManager.IsInRoleAsync((await userManager.FindByEmailAsync(AdminEmail))!, AppRoles.Admin));
    }

    [Fact]
    public async Task Fails_loudly_when_Identity_rejects_the_admin_password()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SeedAsync(new SeedOptions { AdminEmail = AdminEmail, AdminPassword = "short" }));

        Assert.Contains("PasswordTooShort", ex.Message);
    }

    private async Task SeedAsync(SeedOptions options)
    {
        using var scope = _services.CreateScope();
        var sp = scope.ServiceProvider;
        var seeder = new DataSeeder(
            sp.GetRequiredService<AppDbContext>(),
            sp.GetRequiredService<RoleManager<IdentityRole>>(),
            sp.GetRequiredService<UserManager<AppUser>>(),
            Options.Create(options),
            NullLogger<DataSeeder>.Instance,
            _database.Clock);

        await seeder.SeedAsync();
    }
}
