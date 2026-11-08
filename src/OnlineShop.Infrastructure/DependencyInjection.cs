using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OnlineShop.Application.Accounts;
using OnlineShop.Application.Common;
using OnlineShop.Application.Notifications;
using OnlineShop.Infrastructure.Identity;
using OnlineShop.Infrastructure.Notifications;
using OnlineShop.Infrastructure.Persistence;
using OnlineShop.Infrastructure.Persistence.Interceptors;
using OnlineShop.Infrastructure.Persistence.Seeding;

namespace OnlineShop.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseHealthCheckTag = "db";

    /// <param name="persistDataProtectionKeys">
    /// False only for tools that build the host without a database (build-time OpenAPI generation).
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration, bool persistDataProtectionKeys = true)
    {
        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
                              ?? new DatabaseOptions();

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            // Resolved when a context is first created, so tools that only build the host
            // (OpenAPI generation at build time) work without a database.
            var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{DatabaseOptions.ConnectionStringName}' is missing. Set it with dotnet " +
                    "user-secrets (development) or the ConnectionStrings__Default environment variable.");
            ConfigureProvider(options, databaseOptions.Provider, connectionString);
            options.UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });
        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        AddIdentityAndAuth(services, configuration);
        AddEmail(services, configuration);

        // Keys in the database: survive restarts and container replacement, shared by every app instance.
        var dataProtection = services.AddDataProtection().SetApplicationName("OnlineShop");
        if (persistDataProtectionKeys)
            dataProtection.PersistKeysToDbContext<AppDbContext>();

        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.AddScoped<DataSeeder>();

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database", tags: [DatabaseHealthCheckTag]);

        return services;
    }

    /// <summary>Cookie name of the sign-in session (HttpOnly; the browser receives it through the web app's proxy).</summary>
    public const string AuthCookieName = "os_session";

    private static void AddIdentityAndAuth(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddIdentityCore<AppUser>(o =>
            {
                // Email OR phone, each unique when present: enforced by AppUserValidator (+ unique indexes).
                o.User.RequireUniqueEmail = false;
                o.Password.RequiredLength = PasswordRules.MinLength;
                o.Password.RequireDigit = true;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireLowercase = false;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                o.SignIn.RequireConfirmedAccount = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddUserValidator<AppUserValidator>()
            .AddDefaultTokenProviders();

        // Reset/confirmation links stay valid for a short time only.
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(2));

        // Read through IOptions when the cookie handler is first used, not while registering services:
        // configuration sources added later (environment, tests) must be honoured.
        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IOptions<AuthOptions>>((cookie, authOptions) =>
        {
            var auth = authOptions.Value;
            cookie.Cookie.Name = AuthCookieName;
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = SameSiteMode.Lax;
            cookie.Cookie.SecurePolicy = auth.SecureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
            cookie.ExpireTimeSpan = TimeSpan.FromDays(auth.RememberMeDays);
            cookie.SlidingExpiration = true;
            // An API answers with status codes; the web app decides where to send the user.
            cookie.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            cookie.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddSingleton<IPhoneNumberNormalizer, PhoneNumberNormalizer>();
        services.AddScoped<IAccountIdentity, IdentityAccountService>();
    }

    private static void AddEmail(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations();

        services.AddScoped<SmtpEmailSender>();
        services.AddScoped<PickupDirectoryEmailSender>();
        // Chosen per resolution from the bound options (not read once at registration time).
        services.AddScoped<IEmailSender>(sp =>
            sp.GetRequiredService<IOptions<EmailOptions>>().Value.Delivery == EmailDelivery.Smtp
                ? sp.GetRequiredService<SmtpEmailSender>()
                : sp.GetRequiredService<PickupDirectoryEmailSender>());
    }

    internal static void ConfigureProvider(DbContextOptionsBuilder options, DatabaseProvider provider, string connectionString)
    {
        var migrationsAssembly = DatabaseOptions.MigrationsAssemblyFor(provider);
        switch (provider)
        {
            case DatabaseProvider.PostgreSql:
                options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(migrationsAssembly));
                break;
            default:
                throw new NotSupportedException($"Database provider '{provider}' is not implemented yet.");
        }
    }
}
