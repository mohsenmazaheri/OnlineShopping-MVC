using Microsoft.Extensions.Options;
using OnlineShop.Application.Accounts;

namespace OnlineShop.Infrastructure.Identity;

/// <summary>
/// Section <c>Auth:External</c>. A provider is offered only when its credentials are set (secrets belong in
/// user-secrets / environment variables). Iran deployments leave all of them empty.
/// </summary>
public sealed class ExternalLoginOptions
{
    public const string SectionName = "Auth:External";

    public ProviderCredentials Google { get; set; } = new();
    public ProviderCredentials Facebook { get; set; } = new();
    public ProviderCredentials Microsoft { get; set; } = new();

    public sealed class ProviderCredentials
    {
        /// <summary>OAuth client id (Facebook: App ID).</summary>
        public string? ClientId { get; set; }

        /// <summary>OAuth client secret (Facebook: App Secret).</summary>
        public string? ClientSecret { get; set; }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
    }

    public ProviderCredentials For(string provider) => provider switch
    {
        ExternalProviders.Google => Google,
        ExternalProviders.Facebook => Facebook,
        ExternalProviders.Microsoft => Microsoft,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown external provider.")
    };
}

/// <summary>Maps provider slugs to ASP.NET Core authentication scheme names and OAuth callback paths.</summary>
public static class ExternalSchemes
{
    public static string For(string provider) => provider switch
    {
        ExternalProviders.Google => "Google",
        ExternalProviders.Facebook => "Facebook",
        ExternalProviders.Microsoft => "Microsoft",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown external provider.")
    };

    public static string? ProviderForScheme(string scheme) =>
        ExternalProviders.All.FirstOrDefault(p => string.Equals(For(p), scheme, StringComparison.Ordinal));

    /// <summary>
    /// Where the provider sends the browser back. It is under /api/v1 so the storefront's proxy forwards it to the API.
    /// Register https://{shop}/api/v1/account/external/{provider}/oauth-callback in the provider's console.
    /// </summary>
    public static string CallbackPath(string provider) => $"/api/v1/account/external/{provider}/oauth-callback";
}

/// <summary>Providers with credentials, read at runtime (settings can change between deployments of the same image).</summary>
public sealed class ConfiguredExternalProviders(IOptionsMonitor<ExternalLoginOptions> options) : IExternalLoginProviders
{
    public IReadOnlyList<string> Enabled =>
        ExternalProviders.All.Where(p => options.CurrentValue.For(p).IsConfigured).ToList();
}
