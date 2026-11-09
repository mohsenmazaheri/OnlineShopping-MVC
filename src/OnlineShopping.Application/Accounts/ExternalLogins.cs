namespace OnlineShopping.Application.Accounts;

/// <summary>What an external provider (Google, Facebook, Microsoft) told us about the person signing in.</summary>
/// <param name="Provider">Provider slug: google, facebook or microsoft.</param>
/// <param name="ProviderKey">The person's stable id at the provider.</param>
/// <param name="EmailVerified">
/// True only when the provider guarantees the address belongs to the person (Google's <c>email_verified</c>).
/// Only then may the login be attached to an existing account with the same email.
/// </param>
public sealed record ExternalLoginInfo(string Provider, string ProviderKey, string? Email, bool EmailVerified, string? DisplayName);

public sealed record ExternalSignInResult(CurrentUser User, bool IsNewAccount);

/// <summary>Providers this shop offers, and the ones linked to the signed-in account.</summary>
public sealed record ExternalLoginsView(IReadOnlyList<string> Available, IReadOnlyList<string> Linked);

/// <summary>External sign-in providers the system admin configured (credentials present). Slugs: google, facebook, microsoft.</summary>
public interface IExternalLoginProviders
{
    IReadOnlyList<string> Enabled { get; }
}

/// <summary>Provider slugs used in URLs, settings and the web app.</summary>
public static class ExternalProviders
{
    public const string Google = "google";
    public const string Facebook = "facebook";
    public const string Microsoft = "microsoft";

    public static IReadOnlyList<string> All { get; } = [Google, Facebook, Microsoft];
}
