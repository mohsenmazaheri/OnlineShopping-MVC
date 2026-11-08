using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OnlineShop.Application.Accounts;
using OnlineShop.Application.Authorization;
using OnlineShop.Infrastructure.Identity;
using OnlineShop.Infrastructure.Persistence;
using OnlineShop.Infrastructure.Persistence.Interceptors;
using OnlineShop.UnitTests.TestSupport;

namespace OnlineShop.UnitTests.Infrastructure;

public sealed class IdentityRulesTests : IDisposable
{
    private const string Password = "correct horse 42";
    private readonly SqliteTestDatabase _database = new();
    private readonly ServiceProvider _services;

    public IdentityRulesTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_database.Connection).UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditableEntityInterceptor(_database.Clock)));
        services.AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddUserValidator<AppUserValidator>();
        _services = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        _database.Dispose();
    }

    [Fact]
    public async Task An_account_needs_an_email_or_a_phone()
    {
        var result = await CreateAsync(email: null, phone: null);

        Assert.Contains(result.Errors, e => e.Code == AppUserValidator.EmailOrPhoneRequiredCode);
    }

    [Fact]
    public async Task Phone_only_and_email_only_accounts_are_allowed()
    {
        Assert.True((await CreateAsync(email: null, phone: "+989121234567")).Succeeded);
        Assert.True((await CreateAsync(email: "ali@example.com", phone: null)).Succeeded);
    }

    [Fact]
    public async Task Emails_and_phones_are_unique()
    {
        Assert.True((await CreateAsync("ali@example.com", "+989121234567")).Succeeded);

        Assert.Contains((await CreateAsync("ALI@example.com", null)).Errors, e => e.Code == AppUserValidator.DuplicateEmailCode);
        Assert.Contains((await CreateAsync(null, "+989121234567")).Errors, e => e.Code == AppUserValidator.DuplicatePhoneCode);
    }

    [Fact]
    public async Task The_database_also_refuses_duplicate_phones_even_without_the_validator()
    {
        await using var db = _database.CreateContext();
        db.Users.Add(new AppUser { UserName = "a", NormalizedUserName = "A", PhoneNumber = "+989121234567" });
        db.Users.Add(new AppUser { UserName = "b", NormalizedUserName = "B", PhoneNumber = "+989121234567" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Many_accounts_may_have_no_email_or_no_phone()
    {
        await using var db = _database.CreateContext();
        db.Users.Add(new AppUser { UserName = "a", NormalizedUserName = "A", PhoneNumber = "+989121234567" });
        db.Users.Add(new AppUser { UserName = "b", NormalizedUserName = "B", PhoneNumber = "+989121234568" });
        db.Users.Add(new AppUser { UserName = "c", NormalizedUserName = "C", Email = "c@x.io", NormalizedEmail = "C@X.IO" });

        Assert.Equal(3, await db.SaveChangesAsync());
    }

    [Theory]
    [InlineData(AppRoles.Admin, Permissions.StaffManage, true)]
    [InlineData(AppRoles.Warehouse, Permissions.InventoryManage, true)]
    [InlineData(AppRoles.Warehouse, Permissions.AccountingManage, false)]
    [InlineData(AppRoles.Customer, Permissions.OrdersView, false)]
    public async Task Permission_policies_follow_the_role_map(string role, string permission, bool allowed)
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));
        var policy = await provider.GetPolicyAsync(Permissions.Policy(permission));
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));
        // The policy also requires a signed-in user (handled by ASP.NET Core); this checks the permission part.
        Assert.Contains(policy!.Requirements, r => r is Microsoft.AspNetCore.Authorization.Infrastructure.DenyAnonymousAuthorizationRequirement);
        var context = new AuthorizationHandlerContext(policy.Requirements.OfType<PermissionRequirement>(), user, null);

        await new PermissionAuthorizationHandler(new StaticSecurity(new SecurityOptions())).HandleAsync(context);

        Assert.Equal(allowed, context.HasSucceeded);
    }

    [Theory]
    [InlineData(false, false, true)]  // not required: any staff session works
    [InlineData(true, false, false)]  // required: a session without the second step is refused
    [InlineData(true, true, true)]    // required and completed
    public async Task Staff_two_step_requirement_applies_to_permissions(bool required, bool sessionHasMfa, bool allowed)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, AppRoles.Manager) };
        if (sessionHasMfa)
            claims.Add(new Claim(PermissionAuthorizationHandler.MfaClaimType, PermissionAuthorizationHandler.MfaClaimValue));
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        var context = new AuthorizationHandlerContext([new PermissionRequirement(Permissions.CatalogManage)], user, null);

        await new PermissionAuthorizationHandler(new StaticSecurity(new SecurityOptions { RequireStaffTwoFactor = required })).HandleAsync(context);

        Assert.Equal(allowed, context.HasSucceeded);
    }

    [Fact]
    public async Task Unknown_permission_policies_are_a_programming_error()
    {
        var provider = new PermissionPolicyProvider(Options.Create(new AuthorizationOptions()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetPolicyAsync("permission:everything"));
    }

    private async Task<IdentityResult> CreateAsync(string? email, string? phone)
    {
        using var scope = _services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        return await users.CreateAsync(new AppUser { UserName = Guid.NewGuid().ToString("N"), Email = email, PhoneNumber = phone }, Password);
    }
}
