using Microsoft.AspNetCore.Identity;
using OnlineShop.Application.Accounts;

namespace OnlineShop.Infrastructure.Identity;

/// <summary><see cref="ITwoFactorIdentity"/> on ASP.NET Core Identity's authenticator token provider and recovery codes.</summary>
public sealed class TwoFactorIdentity(UserManager<AppUser> users, AppSignInManager signIn) : ITwoFactorIdentity
{
    public const int RecoveryCodeCount = 10;

    public async Task<(bool Enabled, int RecoveryCodesLeft)> GetStatusAsync(string userId)
    {
        var user = await RequireAsync(userId);
        return (await users.GetTwoFactorEnabledAsync(user), await users.CountRecoveryCodesAsync(user));
    }

    public async Task<string> GetOrCreateAuthenticatorKeyAsync(string userId)
    {
        var user = await RequireAsync(userId);
        var key = await users.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await users.ResetAuthenticatorKeyAsync(user);
            key = await users.GetAuthenticatorKeyAsync(user);
        }
        return key!;
    }

    public async Task<bool> VerifyAuthenticatorCodeAsync(string userId, string code)
    {
        var user = await RequireAsync(userId);
        return await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, code);
    }

    public async Task<IReadOnlyList<string>> EnableAsync(string userId)
    {
        var user = await RequireAsync(userId);
        await users.SetTwoFactorEnabledAsync(user, true);
        return (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount))?.ToList() ?? [];
    }

    public async Task DisableAsync(string userId)
    {
        var user = await RequireAsync(userId);
        await users.SetTwoFactorEnabledAsync(user, false);
        await users.ResetAuthenticatorKeyAsync(user);
        // Old recovery codes must not outlive the setting they belong to.
        await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 0);
    }

    public async Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(string userId)
    {
        var user = await RequireAsync(userId);
        return (await users.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount))?.ToList() ?? [];
    }

    public async Task<(SignInStatus Status, string? UserId)> AuthenticatorSignInAsync(string code, bool rememberMe, bool rememberDevice)
    {
        // The pending user comes from the short-lived two-factor cookie set by the first step.
        var user = await signIn.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
            return (SignInStatus.NotAllowed, null);
        return Map(await signIn.TwoFactorAuthenticatorSignInAsync(code, rememberMe, rememberDevice), user.Id);
    }

    public async Task<(SignInStatus Status, string? UserId)> RecoveryCodeSignInAsync(string recoveryCode)
    {
        var user = await signIn.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
            return (SignInStatus.NotAllowed, null);
        return Map(await signIn.TwoFactorRecoveryCodeSignInAsync(recoveryCode), user.Id);
    }

    public async Task<bool> IsDeviceRememberedAsync(string userId) =>
        await users.FindByIdAsync(userId) is { } user && await signIn.IsTwoFactorClientRememberedAsync(user);

    public Task ForgetDeviceAsync() => signIn.ForgetTwoFactorClientAsync();

    private static (SignInStatus, string?) Map(SignInResult result, string userId) => result switch
    {
        { Succeeded: true } => (SignInStatus.Succeeded, userId),
        { IsLockedOut: true } => (SignInStatus.LockedOut, null),
        _ => (SignInStatus.InvalidCredentials, null)
    };

    private async Task<AppUser> RequireAsync(string userId) =>
        await users.FindByIdAsync(userId) ?? throw new InvalidOperationException($"User {userId} does not exist.");
}
