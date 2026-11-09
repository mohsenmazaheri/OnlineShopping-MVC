using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OnlineShopping.Infrastructure.Identity;
using OnlineShopping.Infrastructure.Persistence;
using OnlineShopping.Infrastructure.Persistence.Interceptors;

namespace OnlineShopping.UnitTests.TestSupport;

/// <summary>
/// Real ASP.NET Core Identity (user manager, sign-in manager, cookies) over SQLite, with one request scope and a
/// <see cref="DefaultHttpContext"/> whose response headers show the cookies that were issued.
/// </summary>
internal sealed class IdentityTestHost : IDisposable
{
    private readonly ServiceProvider _services;
    private readonly AsyncServiceScope _scope;

    public IdentityTestHost(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(Database.Clock);
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(Database.Connection).UseSnakeCaseNamingConvention()
            .AddInterceptors(new AuditableEntityInterceptor(Database.Clock)));
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        // Same password rules as the shop (12+ characters with a digit).
        services.AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = false;
                o.Password.RequiredLength = OnlineShopping.Application.Accounts.PasswordRules.MinLength;
                o.Password.RequireUppercase = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager<AppSignInManager>()
            .AddUserValidator<AppUserValidator>()
            .AddDefaultTokenProviders();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = Http });
        configure?.Invoke(services);
        _services = services.BuildServiceProvider();
        _scope = _services.CreateAsyncScope();
        Http.RequestServices = _scope.ServiceProvider;
    }

    public SqliteTestDatabase Database { get; } = new();

    public DefaultHttpContext Http { get; } = new();

    public UserManager<AppUser> Users => Get<UserManager<AppUser>>();

    public AppSignInManager SignIn => Get<AppSignInManager>();

    public T Get<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

    public async Task<AppUser> CreateUserAsync(string? password = null, string? phone = null)
    {
        var name = Guid.NewGuid().ToString("N");
        var user = new AppUser { UserName = name, Email = $"{name}@example.com", PhoneNumber = phone, CreatedUtc = Database.Clock.GetUtcNow().UtcDateTime };
        var result = password is null ? await Users.CreateAsync(user) : await Users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Code)));
        return user;
    }

    /// <summary>Cookies issued so far in this request ("name=value; ...").</summary>
    public IReadOnlyList<string> IssuedCookies => Http.Response.Headers.SetCookie.Select(c => c!).ToList();

    public void Dispose()
    {
        _scope.Dispose();
        _services.Dispose();
        Database.Dispose();
    }
}
