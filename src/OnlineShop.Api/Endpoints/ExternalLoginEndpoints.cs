using System.Security.Claims;
using Microsoft.Extensions.Options;
using OnlineShop.Api.Common;
using OnlineShop.Api.Security;
using OnlineShop.Application.Accounts;
using OnlineShop.Application.Common;
using OnlineShop.Infrastructure.Identity;

namespace OnlineShop.Api.Endpoints;

/// <summary>
/// Sign-in with Google, Facebook or Microsoft. The browser navigates (top-level GET) to /start; the provider round trip
/// is protected by the OAuth state and correlation cookie; the callback signs the user in and redirects back into the
/// storefront. Linking works the same way for a signed-in user.
/// </summary>
public static class ExternalLoginEndpoints
{
    private const string Base = "/api/v1/account/external";

    public static IEndpointRouteBuilder MapExternalLoginEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Base).WithTags("Account");

        group.MapGet("/", async (ClaimsPrincipal user, ExternalLoginService logins) =>
                (await logins.GetAsync(user.GetUserId())).ToHttpResult())
            .RequireAuthorization()
            .WithName("GetExternalLogins")
            .WithSummary("Providers this shop offers and the ones linked to the signed-in account.")
            .Produces<ExternalLoginsView>();

        group.MapGet("/{provider}/start", (string provider, string? returnUrl, string? locale,
                IExternalLoginProviders providers, ExternalAuthGateway gateway, IOptions<StoreOptions> store) =>
            {
                if (!providers.Enabled.Contains(provider))
                    return Results.NotFound();
                var loc = ReturnPaths.Locale(locale, store.Value);
                var target = ReturnPaths.Sanitize(returnUrl, loc, store.Value);
                var callback = $"{Base}/callback?locale={loc}&returnUrl={Uri.EscapeDataString(target)}";
                return Results.Challenge(gateway.Challenge(provider, callback), [ExternalSchemes.For(provider)]);
            })
            .RequireRateLimiting(AccountEndpoints.AuthRateLimitPolicy)
            .WithName("StartExternalSignIn")
            .WithSummary("Browser navigation: redirects to the provider's sign-in page.");

        group.MapGet("/callback", async (string? returnUrl, string? locale, ExternalAuthGateway gateway,
                ExternalLoginService logins, IOptions<StoreOptions> store, CancellationToken ct) =>
            {
                var loc = ReturnPaths.Locale(locale, store.Value);
                var info = await gateway.ReadAsync();
                if (info is null)
                    return Results.Redirect(ReturnPaths.LoginError(loc, AccountErrors.ExternalFailed));

                var result = await logins.SignInAsync(info, loc, ct);
                await gateway.ClearAsync();
                if (!result.IsSuccess)
                    return Results.Redirect(ReturnPaths.LoginError(loc, result.Error!.Code));
                return Results.Redirect(result.Value.IsNewAccount
                    ? ReturnPaths.AccountPage(loc, "welcome=1")
                    : ReturnPaths.Sanitize(returnUrl, loc, store.Value));
            })
            .WithName("ExternalSignInCallback")
            .WithSummary("Browser navigation: end of the provider round trip; signs in and redirects into the storefront.");

        group.MapGet("/{provider}/link", (string provider, string? locale, ClaimsPrincipal user,
                IExternalLoginProviders providers, ExternalAuthGateway gateway, IOptions<StoreOptions> store) =>
            {
                if (!providers.Enabled.Contains(provider))
                    return Results.NotFound();
                var loc = ReturnPaths.Locale(locale, store.Value);
                var callback = $"{Base}/link-callback?locale={loc}";
                return Results.Challenge(gateway.Challenge(provider, callback, user.GetUserId()), [ExternalSchemes.For(provider)]);
            })
            .RequireAuthorization()
            .RequireRateLimiting(AccountEndpoints.AuthRateLimitPolicy)
            .WithName("StartExternalLink")
            .WithSummary("Browser navigation: link a provider to the signed-in account.");

        group.MapGet("/link-callback", async (string? locale, ClaimsPrincipal user, ExternalAuthGateway gateway,
                ExternalLoginService logins, IOptions<StoreOptions> store) =>
            {
                var loc = ReturnPaths.Locale(locale, store.Value);
                // The round trip was started for this user only (Identity checks the id stored in the state).
                var info = await gateway.ReadAsync(user.GetUserId());
                if (info is null)
                    return Results.Redirect(ReturnPaths.AccountPage(loc, "error=" + ReturnPaths.ShortCode(AccountErrors.ExternalFailed)));

                var result = await logins.LinkAsync(user.GetUserId(), info);
                await gateway.ClearAsync();
                return Results.Redirect(result.IsSuccess
                    ? ReturnPaths.AccountPage(loc, "linked=" + info.Provider)
                    : ReturnPaths.AccountPage(loc, "error=" + ReturnPaths.ShortCode(result.Error!.Code)));
            })
            .RequireAuthorization()
            .WithName("ExternalLinkCallback")
            .WithSummary("Browser navigation: end of the link round trip.");

        group.MapPost("/{provider}/unlink", async (string provider, ClaimsPrincipal user, ExternalLoginService logins) =>
                (await logins.UnlinkAsync(user.GetUserId(), provider)).ToHttpResult())
            .RequireAuthorization()
            .WithName("UnlinkExternalLogin")
            .WithSummary("Unlink a provider (refused when it is the account's only way to sign in).")
            .Produces<ExternalLoginsView>();

        return app;
    }
}
