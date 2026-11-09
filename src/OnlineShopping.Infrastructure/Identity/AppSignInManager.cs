using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OnlineShopping.Application.Accounts;

namespace OnlineShopping.Infrastructure.Identity;

/// <summary>
/// Identity's SignInManager with two additions:
/// <list type="bullet">
/// <item>signing in a user who proved a first factor other than a password (an SMS code, an external provider) while
/// still honouring two-step sign-in — the built-in SignInAsync always bypasses the second step;</item>
/// <item>an <see cref="AccountClaims.AuthTime"/> claim on every new session, kept when the session is refreshed, so
/// sensitive actions can ask "did the user sign in recently?".</item>
/// </list>
/// </summary>
public sealed class AppSignInManager(
    UserManager<AppUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<AppUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<AppUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<AppUser> confirmation,
    TimeProvider clock)
    : SignInManager<AppUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    /// <summary>Signs in, or starts the second step when the account has two-step sign-in on and this device is not remembered.</summary>
    public Task<SignInResult> SignInWithSecondStepAsync(AppUser user, bool isPersistent) =>
        SignInOrTwoFactorAsync(user, isPersistent, loginProvider: null, bypassTwoFactor: false);

    /// <summary>Every sign-in path (password, code, provider, second step) ends here: a new session gets the current time.</summary>
    public override Task SignInWithClaimsAsync(AppUser user, AuthenticationProperties? authenticationProperties, IEnumerable<Claim> additionalClaims)
    {
        var claims = additionalClaims.ToList();
        if (!claims.Exists(c => c.Type == AccountClaims.AuthTime))
            claims.Add(SignInClaims.AuthTimeClaim(clock.GetUtcNow()));
        return base.SignInWithClaimsAsync(user, authenticationProperties, claims);
    }

    /// <summary>Re-issues the cookie (after a profile or password change) without pretending the user just signed in.</summary>
    public override async Task RefreshSignInAsync(AppUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var auth = await Context.AuthenticateAsync(AuthenticationScheme);
        if (!auth.Succeeded || auth.Principal is null || UserManager.GetUserId(auth.Principal) != await UserManager.GetUserIdAsync(user))
            return;
        await base.SignInWithClaimsAsync(user, auth.Properties, SignInClaims.Carried(auth.Principal));
    }
}

/// <summary>Claims that describe the sign-in itself (not the user) and must survive a session refresh.</summary>
public static class SignInClaims
{
    private static readonly string[] CarriedTypes = [AccountClaims.AuthTime, PermissionAuthorizationHandler.MfaClaimType, ClaimTypes.AuthenticationMethod];

    public static Claim AuthTimeClaim(DateTimeOffset at) =>
        new(AccountClaims.AuthTime, at.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64);

    /// <summary>The sign-in claims of <paramref name="principal"/> (auth time, amr=mfa, provider).</summary>
    public static IReadOnlyList<Claim> Carried(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.Claims.Where(c => CarriedTypes.Contains(c.Type)).Select(c => new Claim(c.Type, c.Value, c.ValueType)).ToList();
    }

    /// <summary>
    /// Identity rebuilds the principal from the database every few minutes (security stamp check) and would drop these
    /// claims: a two-step session would silently lose its amr=mfa and staff would lose their permissions.
    /// </summary>
    public static Task PreserveOnRefresh(SecurityStampRefreshingPrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.CurrentPrincipal is { } current && context.NewPrincipal?.Identity is ClaimsIdentity identity)
        {
            foreach (var claim in Carried(current).Where(c => !identity.HasClaim(c.Type, c.Value)))
                identity.AddClaim(claim);
        }
        return Task.CompletedTask;
    }

    /// <summary>When the session's sign-in happened, or null for sessions issued before the claim existed.</summary>
    public static DateTimeOffset? AuthTime(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return long.TryParse(principal.FindFirstValue(AccountClaims.AuthTime), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : null;
    }
}
