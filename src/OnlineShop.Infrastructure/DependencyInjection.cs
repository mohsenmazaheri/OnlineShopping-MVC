using Microsoft.AspNetCore.Authentication;
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
        AddSms(services, configuration);
        AddExternalLogins(services, configuration);

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
            .AddSignInManager<AppSignInManager>()
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
        services.AddScoped<ITwoFactorIdentity, TwoFactorIdentity>();
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

    private static void AddExternalLogins(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ExternalLoginOptions>().Bind(configuration.GetSection(ExternalLoginOptions.SectionName));
        services.AddSingleton<IExternalLoginProviders, ConfiguredExternalProviders>();
        services.AddHttpContextAccessor();
        services.AddScoped<ExternalAuthGateway>();

        // All three handlers are always registered (the image is shared by every shop); a provider without
        // credentials gets a placeholder and is never offered or challenged (see IExternalLoginProviders).
        services.AddAuthentication()
            .AddGoogle(ExternalSchemes.For(ExternalProviders.Google), _ => { })
            .AddFacebook(ExternalSchemes.For(ExternalProviders.Facebook), _ => { })
            .AddMicrosoftAccount(ExternalSchemes.For(ExternalProviders.Microsoft), _ => { });

        static void Common(RemoteAuthenticationOptions o, string provider, ExternalLoginOptions ext, AuthOptions auth)
        {
            o.SignInScheme = IdentityConstants.ExternalScheme;
            o.CallbackPath = ExternalSchemes.CallbackPath(provider);
            // Lax works for the provider's top-level redirect back, and also on plain-HTTP test instances.
            o.CorrelationCookie.SameSite = SameSiteMode.Lax;
            o.CorrelationCookie.SecurePolicy = auth.SecureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
            // Cancelled at the provider, expired or forged state: back to the login page with a message instead of a 500.
            o.Events.OnRemoteFailure = context =>
            {
                var locale = context.HttpContext.RequestServices.GetRequiredService<IOptions<OnlineShop.Application.Common.StoreOptions>>().Value.DefaultLocale;
                context.Response.Redirect($"/{locale}/account/login?error=external_failed");
                context.HandleResponse();
                return Task.CompletedTask;
            };
        }

        services.AddOptions<Microsoft.AspNetCore.Authentication.Google.GoogleOptions>(ExternalSchemes.For(ExternalProviders.Google))
            .Configure<IOptions<ExternalLoginOptions>, IOptions<AuthOptions>>((o, ext, auth) =>
            {
                var c = ext.Value.Google;
                o.ClientId = c.IsConfigured ? c.ClientId! : "not-configured";
                o.ClientSecret = c.IsConfigured ? c.ClientSecret! : "not-configured";
                o.ClaimActions.MapJsonKey("email_verified", "email_verified");
                Common(o, ExternalProviders.Google, ext.Value, auth.Value);
            });
        services.AddOptions<Microsoft.AspNetCore.Authentication.Facebook.FacebookOptions>(ExternalSchemes.For(ExternalProviders.Facebook))
            .Configure<IOptions<ExternalLoginOptions>, IOptions<AuthOptions>>((o, ext, auth) =>
            {
                var c = ext.Value.Facebook;
                o.AppId = c.IsConfigured ? c.ClientId! : "not-configured";
                o.AppSecret = c.IsConfigured ? c.ClientSecret! : "not-configured";
                Common(o, ExternalProviders.Facebook, ext.Value, auth.Value);
            });
        services.AddOptions<Microsoft.AspNetCore.Authentication.MicrosoftAccount.MicrosoftAccountOptions>(ExternalSchemes.For(ExternalProviders.Microsoft))
            .Configure<IOptions<ExternalLoginOptions>, IOptions<AuthOptions>>((o, ext, auth) =>
            {
                var c = ext.Value.Microsoft;
                o.ClientId = c.IsConfigured ? c.ClientId! : "not-configured";
                o.ClientSecret = c.IsConfigured ? c.ClientSecret! : "not-configured";
                Common(o, ExternalProviders.Microsoft, ext.Value, auth.Value);
            });
    }

    private static void AddSms(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SmsOptions>()
            .Bind(configuration.GetSection(SmsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Replaces the Application default (no SMS): on whenever a provider is configured.
        services.AddSingleton<ISmsCapability, ConfiguredSmsCapability>();

        static void Http(HttpClient client, string baseAddress)
        {
            client.BaseAddress = new Uri(baseAddress);
            client.Timeout = TimeSpan.FromSeconds(15);
        }
        services.AddHttpClient<KavenegarSmsSender>(c => Http(c, KavenegarSmsSender.BaseAddress));
        services.AddHttpClient<SmsIrSmsSender>(c => Http(c, SmsIrSmsSender.BaseAddress));
        services.AddHttpClient<TwilioSmsSender>(c => Http(c, TwilioSmsSender.BaseAddress));
        services.AddScoped<PickupSmsSender>();

        // Chosen per resolution from the bound options (not read once at registration time).
        services.AddScoped<ISmsSender>(sp => sp.GetRequiredService<IOptions<SmsOptions>>().Value.Provider switch
        {
            SmsProvider.Pickup => sp.GetRequiredService<PickupSmsSender>(),
            SmsProvider.Kavenegar => sp.GetRequiredService<KavenegarSmsSender>(),
            SmsProvider.SmsIr => sp.GetRequiredService<SmsIrSmsSender>(),
            SmsProvider.Twilio => sp.GetRequiredService<TwilioSmsSender>(),
            _ => new DisabledSmsSender()
        });
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
