using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OnlineShop.Infrastructure.Identity;

/// <summary>
/// Identity's SignInManager plus one capability: signing in a user who proved a first factor other than a password
/// (an SMS code, an external provider) while still honouring two-step sign-in. The built-in SignInAsync always
/// bypasses the second step.
/// </summary>
public sealed class AppSignInManager(
    UserManager<AppUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<AppUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<AppUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<AppUser> confirmation)
    : SignInManager<AppUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
{
    /// <summary>Signs in, or starts the second step when the account has two-step sign-in on and this device is not remembered.</summary>
    public Task<SignInResult> SignInWithSecondStepAsync(AppUser user, bool isPersistent) =>
        SignInOrTwoFactorAsync(user, isPersistent, loginProvider: null, bypassTwoFactor: false);
}
