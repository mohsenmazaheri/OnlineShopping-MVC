using Microsoft.Extensions.Options;
using OnlineShop.Application.Common;
using OnlineShop.Application.Notifications;

namespace OnlineShop.Application.Accounts;

/// <summary>
/// Sign-in with Google, Facebook or Microsoft, and linking/unlinking those logins.
/// Security rule: an external login is attached to an existing account automatically only when the provider
/// guarantees the email is verified; otherwise anyone who registers that address at the provider could take over the
/// account. The user then signs in another way and links the provider from their account page.
/// </summary>
public sealed class ExternalLoginService(
    IAccountIdentity identity,
    IExternalLoginProviders providers,
    AccountService accounts,
    ISmsCapability sms,
    IOptions<AuthOptions> authOptions)
{
    public async Task<Result<ExternalSignInResult>> SignInAsync(ExternalLoginInfo info, string? locale, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (!providers.Enabled.Contains(info.Provider))
            return Disabled();

        var userId = await identity.FindUserIdByExternalLoginAsync(info.Provider, info.ProviderKey);
        var isNew = false;

        if (userId is null)
        {
            if (string.IsNullOrWhiteSpace(info.Email))
                return AppError.Validation(AccountErrors.ExternalNoEmail, "The provider did not share an email address.");

            var existing = await identity.FindUserIdByEmailAsync(info.Email);
            if (existing is not null)
            {
                if (!info.EmailVerified)
                    return AppError.Conflict(AccountErrors.ExternalEmailInUse,
                        "An account with this email exists. Sign in to it, then link this provider from your account.");
                var linked = await identity.AddExternalLoginAsync(existing, info.Provider, info.ProviderKey, info.DisplayName);
                if (!linked.Succeeded)
                    return AppError.Conflict(AccountErrors.ExternalLoginInUse, "This provider account is linked to another account.");
                userId = existing;
            }
            else
            {
                if (!authOptions.Value.EmailSignUp)
                    return AppError.Validation(AccountErrors.EmailSignUpDisabled, "Sign-up with email is not available.");

                var resolvedLocale = accounts.ResolveLocale(locale);
                var created = await identity.CreateExternalCustomerAsync(info.Email, info.EmailVerified, info.DisplayName, resolvedLocale);
                if (!created.Succeeded)
                    return AppError.Conflict(AccountErrors.EmailTaken, "An account with this email already exists.");
                userId = created.UserId!;

                var linked = await identity.AddExternalLoginAsync(userId, info.Provider, info.ProviderKey, info.DisplayName);
                if (!linked.Succeeded)
                    return AppError.Conflict(AccountErrors.ExternalLoginInUse, "This provider account is linked to another account.");
                if (!info.EmailVerified)
                    await accounts.SendConfirmationAsync(userId, info.Email, resolvedLocale, cancellationToken);
                isNew = true;
            }
        }

        if (!isNew && await identity.IsLockedOutAsync(userId))
            return AppError.Forbidden(AccountErrors.LockedOut, "Too many failed attempts. Try again later.");

        // An account with two-step sign-in needs the authenticator after the provider too.
        if (await identity.SignInAsync(userId, rememberMe: true) == SignInStatus.TwoFactorRequired)
            return AccountService.TwoFactorStepRequired();
        var user = await identity.FindAsync(userId);
        return user is null ? AppError.NotFound(AccountErrors.NotFound, "Account not found.") : new ExternalSignInResult(user, isNew);
    }

    /// <summary>Links a provider to the signed-in account (the user proved both by being signed in and by the provider round trip).</summary>
    public async Task<Result<bool>> LinkAsync(string userId, ExternalLoginInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (!providers.Enabled.Contains(info.Provider))
            return Disabled<bool>();

        var owner = await identity.FindUserIdByExternalLoginAsync(info.Provider, info.ProviderKey);
        if (owner == userId)
            return true;
        if (owner is not null)
            return AppError.Conflict(AccountErrors.ExternalLoginInUse, "This provider account is linked to another account.");

        var linked = await identity.AddExternalLoginAsync(userId, info.Provider, info.ProviderKey, info.DisplayName);
        return linked.Succeeded
            ? true
            : AppError.Conflict(AccountErrors.ExternalLoginInUse, "This provider account is linked to another account.");
    }

    /// <summary>Unlinks a provider, unless it is the account's only way to sign in.</summary>
    public async Task<Result<ExternalLoginsView>> UnlinkAsync(string userId, string provider)
    {
        var linked = await identity.GetExternalLoginsAsync(userId);
        if (!linked.Contains(provider))
            return AppError.NotFound(AccountErrors.ExternalNotLinked, "This provider is not linked.");

        var user = await identity.FindAsync(userId);
        var hasOtherMethod = linked.Count > 1
                             || await identity.HasPasswordAsync(userId)
                             || (user?.PhoneNumberConfirmed == true && sms.IsEnabled);
        if (!hasOtherMethod)
            return AppError.Validation(AccountErrors.ExternalLastMethod,
                "This is your only way to sign in. Set a password or add a phone first.");

        var removed = await identity.RemoveExternalLoginAsync(userId, provider);
        if (!removed.Succeeded)
            return AppError.NotFound(AccountErrors.ExternalNotLinked, "This provider is not linked.");
        return await GetAsync(userId);
    }

    public async Task<Result<ExternalLoginsView>> GetAsync(string userId) =>
        new ExternalLoginsView(providers.Enabled, await identity.GetExternalLoginsAsync(userId));

    private static AppError Disabled() =>
        AppError.NotFound(AccountErrors.ExternalProviderDisabled, "This sign-in provider is not available.");

    private static Result<T> Disabled<T>() => Result.Failure<T>(Disabled());
}
