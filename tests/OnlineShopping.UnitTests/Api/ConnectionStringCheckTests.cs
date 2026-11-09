using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using OnlineShopping.Api.Common;

namespace OnlineShopping.UnitTests.Api;

public sealed class ConnectionStringCheckTests
{
    private static IConfiguration Config(string? connectionString) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = connectionString })
            .Build();

    private static HostingEnvironment Env(string name) => new() { EnvironmentName = name };

    [Fact]
    public void A_configured_connection_string_passes() =>
        Assert.Null(ConnectionStringCheck.Problem(Config("Host=db;Database=shop"), Env(Environments.Production), null));

    [Fact]
    public void Production_points_at_the_environment_variable()
    {
        var problem = ConnectionStringCheck.Problem(Config(null), Env(Environments.Production), "some-id");

        Assert.Contains("ConnectionStrings__Default", problem, StringComparison.Ordinal);
        Assert.DoesNotContain("user-secrets", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Development_names_the_user_secrets_file_and_whether_it_exists()
    {
        var id = $"connection-check-{Guid.NewGuid():N}";

        var problem = ConnectionStringCheck.Problem(Config(" "), Env(Environments.Development), id);

        Assert.Contains(PathHelper.GetSecretsPathFromSecretsId(id), problem, StringComparison.Ordinal);
        Assert.Contains("was not found", problem, StringComparison.Ordinal);
        Assert.Contains("dotnet user-secrets set", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Development_says_when_the_file_exists_without_the_key()
    {
        var id = $"connection-check-{Guid.NewGuid():N}";
        var path = PathHelper.GetSecretsPathFromSecretsId(id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ }");
        try
        {
            var problem = ConnectionStringCheck.Problem(Config(null), Env(Environments.Development), id);

            Assert.Contains("exists but did not provide 'ConnectionStrings:Default'", problem, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
