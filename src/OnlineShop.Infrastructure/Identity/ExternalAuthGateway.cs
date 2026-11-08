using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using AppExternalLoginInfo = OnlineShop.Application.Accounts.ExternalLoginInfo;
using IdentityExternalLoginInfo = Microsoft.AspNetCore.Identity.ExternalLoginInfo;

namespace OnlineShop.Infrastructure.Identity;

/// <summary>The ASP.NET Core side of external sign-in: starting the provider round trip and reading its result.</summary>
public sealed class ExternalAuthGateway(SignInManager<AppUser> signIn, IHttpContextAccessor http)
{
    /// <summary>Properties for a challenge. With <paramref name="userId"/> the round trip may only link to that user.</summary>
    public AuthenticationProperties Challenge(string provider, string redirectUrl, string? userId = null) =>
        signIn.ConfigureExternalAuthenticationProperties(ExternalSchemes.For(provider), redirectUrl, userId);

    /// <summary>The login the provider returned (from the temporary external cookie), or null when there is none or it was tampered with.</summary>
    public async Task<AppExternalLoginInfo?> ReadAsync(string? expectedUserId = null)
    {
        var info = await signIn.GetExternalLoginInfoAsync(expectedUserId);
        return info is null ? null : Map(info);
    }

    /// <summary>Drops the temporary external cookie once its information has been used.</summary>
    public Task ClearAsync() =>
        http.HttpContext!.SignOutAsync(IdentityConstants.ExternalScheme);

    internal static AppExternalLoginInfo? Map(IdentityExternalLoginInfo info)
    {
        var provider = ExternalSchemes.ProviderForScheme(info.LoginProvider);
        if (provider is null)
            return null;

        var principal = info.Principal;
        var email = principal.FindFirstValue(ClaimTypes.Email);
        // Only Google states that the address is verified; Facebook and Microsoft accounts can carry unverified emails.
        var verified = provider == Application.Accounts.ExternalProviders.Google
                       && string.Equals(principal.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase);
        var name = principal.FindFirstValue(ClaimTypes.Name);
        return new AppExternalLoginInfo(provider, info.ProviderKey, string.IsNullOrWhiteSpace(email) ? null : email, verified,
            string.IsNullOrWhiteSpace(name) ? null : name);
    }
}
