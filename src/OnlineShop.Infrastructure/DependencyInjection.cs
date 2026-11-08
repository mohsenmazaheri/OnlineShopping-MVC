using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OnlineShop.Application.Common;
using OnlineShop.Infrastructure.Identity;
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

        services.AddIdentityCore<AppUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 12;
                o.Password.RequireDigit = true;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequireUppercase = false;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

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
