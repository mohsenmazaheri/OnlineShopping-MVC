using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OnlineShop.Application.Notifications;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace OnlineShop.IntegrationTests.TestSupport;

/// <summary>
/// Runs the real API against a fresh PostgreSQL database that is created for the test class and dropped afterwards.
/// The server comes from <c>IntegrationTests:AdminConnection</c> (user-secrets locally) or the
/// <c>INTEGRATIONTESTS__ADMINCONNECTION</c> environment variable (CI); that login needs CREATEDB.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"onlineshop_it_{Guid.NewGuid():N}";
    private readonly string _adminConnection = ReadAdminConnection();

    public string ConnectionString => new NpgsqlConnectionStringBuilder(_adminConnection) { Database = _databaseName }.ConnectionString;

    /// <summary>Extra configuration for a test class (e.g. tight rate limits).</summary>
    protected virtual IReadOnlyDictionary<string, string?> ConfigurationOverrides => new Dictionary<string, string?>();

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\" ENCODING 'UTF8' TEMPLATE template0", connection);
        await command.ExecuteNonQueryAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // "Testing": no developer user-secrets are loaded, so nothing points at the dev database.
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = ConnectionString,
                ["Database:MigrateOnStartup"] = "true",
                ["Store:Name"] = "Test Shop",
                ["Store:DefaultLocale"] = "fa",
                ["Store:Locales"] = "fa,en",
                ["Store:PublicUrl"] = PublicUrl,
                ["Store:PhoneRegion"] = "IR",
                // The test client talks plain HTTP.
                ["Auth:SecureCookies"] = "false",
                // Account tests sign in many times; the auth limit itself is tested separately.
                ["RateLimiting:AuthPermitPerWindow"] = "1000"
            };
            foreach (var (key, value) in ConfigurationOverrides)
                settings[key] = value;
            config.AddInMemoryCollection(settings);
        });

        // Capture emails instead of writing files.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }

    /// <summary>Plain HTTP like the test client: an https public address would make the API mark cookies Secure.</summary>
    public const string PublicUrl = "http://shop.test";

    /// <summary>Emails the API sent during this test class.</summary>
    public CapturingEmailSender Emails { get; } = new();

    private static string ReadAdminConnection()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<ApiFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        return configuration["IntegrationTests:AdminConnection"]
            ?? throw new InvalidOperationException(
                "Integration tests need a PostgreSQL login with CREATEDB. Set IntegrationTests:AdminConnection with " +
                "'dotnet user-secrets set IntegrationTests:AdminConnection \"Host=...;Username=...;Password=...\" " +
                "--project tests/OnlineShop.IntegrationTests' or the INTEGRATIONTESTS__ADMINCONNECTION environment variable.");
    }
}
