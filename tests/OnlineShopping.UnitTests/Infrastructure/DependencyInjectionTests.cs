using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OnlineShopping.Application.Common;
using OnlineShopping.Infrastructure;
using OnlineShopping.Infrastructure.Persistence;

namespace OnlineShopping.UnitTests.Infrastructure;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void The_application_db_context_is_the_infrastructure_AppDbContext()
    {
        using var services = Build(withConnectionString: true);
        using var scope = services.CreateScope();

        var applicationDb = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        Assert.Same(scope.ServiceProvider.GetRequiredService<AppDbContext>(), applicationDb);
    }

    [Fact]
    public void Password_policy_requires_eight_characters_and_locks_out_after_five_failures()
    {
        using var services = Build(withConnectionString: true);

        var identity = services.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.Equal(8, identity.Password.RequiredLength);
        Assert.True(identity.Password.RequireDigit);
        // Email OR phone: uniqueness is enforced by AppUserValidator and unique indexes instead.
        Assert.False(identity.User.RequireUniqueEmail);
        Assert.True(identity.Lockout.AllowedForNewUsers);
        Assert.Equal(5, identity.Lockout.MaxFailedAccessAttempts);
    }

    [Fact]
    public void A_missing_connection_string_fails_with_a_helpful_message_when_the_database_is_first_used()
    {
        using var services = Build(withConnectionString: false);
        using var scope = services.CreateScope();

        var ex = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<AppDbContext>());

        Assert.Contains("user-secrets", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PostgreSql_migrations_live_in_their_own_assembly()
    {
        Assert.Equal("OnlineShopping.Infrastructure.Migrations.PostgreSql",
            DatabaseOptions.MigrationsAssemblyFor(DatabaseProvider.PostgreSql));
        Assert.Equal(typeof(OnlineShopping.Infrastructure.Migrations.PostgreSql.Migrations.InitialCreate).Assembly.GetName().Name,
            DatabaseOptions.MigrationsAssemblyFor(DatabaseProvider.PostgreSql));
    }

    [Theory]
    [InlineData(DatabaseProvider.SqlServer)]
    [InlineData(DatabaseProvider.MySql)]
    public void Planned_providers_fail_clearly_until_implemented(DatabaseProvider provider)
    {
        var ex = Assert.Throws<NotSupportedException>(() => DatabaseOptions.MigrationsAssemblyFor(provider));

        Assert.Contains("PostgreSql", ex.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider Build(bool withConnectionString)
    {
        var settings = new Dictionary<string, string?> { ["Database:Provider"] = "PostgreSql" };
        if (withConnectionString)
            settings["ConnectionStrings:Default"] = "Host=localhost;Database=never_opened;Username=test";

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(configuration, persistDataProtectionKeys: false)
            .BuildServiceProvider();
    }
}
