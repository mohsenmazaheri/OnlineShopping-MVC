using Microsoft.Extensions.Configuration.UserSecrets;
using OnlineShopping.Infrastructure.Persistence;

namespace OnlineShopping.Api.Common;

/// <summary>
/// Fails at startup, with directions, when the database connection string is not configured — instead of a
/// later, confusing database error. In Development the message names the exact user-secrets file the app read.
/// </summary>
internal static class ConnectionStringCheck
{
    /// <returns>Null when the connection string is set; otherwise the message to stop with.</returns>
    public static string? Problem(IConfiguration configuration, IHostEnvironment environment, string? userSecretsId)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString(DatabaseOptions.ConnectionStringName)))
            return null;

        var key = $"ConnectionStrings:{DatabaseOptions.ConnectionStringName}";
        if (!environment.IsDevelopment() || string.IsNullOrEmpty(userSecretsId))
            return $"'{key}' is not configured. Set the ConnectionStrings__{DatabaseOptions.ConnectionStringName} environment variable.";

        var path = PathHelper.GetSecretsPathFromSecretsId(userSecretsId);
        var where = File.Exists(path)
            ? $"{path} exists but did not provide '{key}' (the key is missing, misspelled or empty)"
            : $"{path} was not found";
        return $"'{key}' is not configured. In Development it is read from user-secrets: {where}. " +
               $"Set it with: dotnet user-secrets set \"{key}\" \"Host=localhost;Database=onlineshop_dev;Username=onlineshop;Password=...\" " +
               "--project src/OnlineShopping.Api";
    }
}
