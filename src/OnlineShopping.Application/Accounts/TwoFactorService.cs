using Microsoft.Extensions.Options;
using OnlineShopping.Application.Common;

namespace OnlineShopping.Application.Accounts;

/// <summary>
/// Two-step sign-in with an authenticator app (TOTP) and recovery codes.
/// Turning it off and renewing recovery codes need a current authenticator code, so a stolen session alone
/// cannot remove the protection. Staff cannot turn it off while <see cref="SecurityOptions.RequireStaffTwoFactor"/> is on.
/// </summary>
public sealed class TwoFactorService(
    ITwoFactorIdentity twoFactor,
    IAccountIdentity identity,
    IOptions<StoreOptions> store,
    IOptionsMonitor<SecurityOptions> security)
{
    public async Task<Result<TwoFactorStatus>> GetStatusAsync(string userId)
    {
        var (enabled, left) = await twoFactor.GetStatusAsync(userId);
        return new TwoFactorStatus(enabled, left, await twoFactor.IsDeviceRememberedAsync(userId), await IsRequiredAsync(userId));
    }

    /// <summary>The key and otpauth:// URI for the authenticator app. Refused while two-step sign-in is already on.</summary>
    public async Task<Result<AuthenticatorSetup>> BeginSetupAsync(string userId)
    {
        var (enabled, _) = await twoFactor.GetStatusAsync(userId);
        if (enabled)
            return AppError.Conflict(AccountErrors.TwoFactorAlreadyEnabled, "Two-step sign-in is already on.");

        var key = await twoFactor.GetOrCreateAuthenticatorKeyAsync(userId);
        var user = await identity.FindAsync(userId);
        var label = user?.Email ?? user?.PhoneNumber ?? user?.DisplayName ?? userId;
        return new AuthenticatorSetup(FormatKey(key), AuthenticatorUri(store.Value.Name, label, key));
    }

    /// <summary>Turns it on once the user proves the app works with a first code; returns the recovery codes (shown once).</summary>
    public async Task<Result<RecoveryCodes>> EnableAsync(string userId, string code)
    {
        var (enabled, _) = await twoFactor.GetStatusAsync(userId);
        if (enabled)
            return AppError.Conflict(AccountErrors.TwoFactorAlreadyEnabled, "Two-step sign-in is already on.");
        if (!await twoFactor.VerifyAuthenticatorCodeAsync(userId, Clean(code)))
            return AppError.Validation(AccountErrors.CodeInvalid, "The code is not correct.");

        return new RecoveryCodes(await twoFactor.EnableAsync(userId));
    }

    public async Task<Result<bool>> DisableAsync(string userId, string code)
    {
        var (enabled, _) = await twoFactor.GetStatusAsync(userId);
        if (!enabled)
            return true;
        if (await IsRequiredAsync(userId))
            return AppError.Forbidden(AccountErrors.TwoFactorRequiredForStaff, "Staff accounts must keep two-step sign-in on.");
        if (!await twoFactor.VerifyAuthenticatorCodeAsync(userId, Clean(code)))
            return AppError.Validation(AccountErrors.CodeInvalid, "The code is not correct.");

        await twoFactor.DisableAsync(userId);
        return true;
    }

    public async Task<Result<RecoveryCodes>> RegenerateRecoveryCodesAsync(string userId, string code)
    {
        var (enabled, _) = await twoFactor.GetStatusAsync(userId);
        if (!enabled)
            return AppError.Validation(AccountErrors.TwoFactorNotEnabled, "Two-step sign-in is off.");
        if (!await twoFactor.VerifyAuthenticatorCodeAsync(userId, Clean(code)))
            return AppError.Validation(AccountErrors.CodeInvalid, "The code is not correct.");

        return new RecoveryCodes(await twoFactor.RegenerateRecoveryCodesAsync(userId));
    }

    public async Task<Result<CurrentUser>> SignInWithAuthenticatorAsync(TwoFactorSignInRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await CompleteAsync(await twoFactor.AuthenticatorSignInAsync(Clean(request.Code), request.RememberMe, request.RememberDevice));
    }

    public async Task<Result<CurrentUser>> SignInWithRecoveryCodeAsync(RecoveryCodeSignInRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Recovery codes are shown with a dash in the middle; users may type them with or without it, in any case.
        return await CompleteAsync(await twoFactor.RecoveryCodeSignInAsync(request.RecoveryCode.Trim().Replace(" ", "", StringComparison.Ordinal)));
    }

    public Task ForgetDeviceAsync() => twoFactor.ForgetDeviceAsync();

    private async Task<Result<CurrentUser>> CompleteAsync((SignInStatus Status, string? UserId) result) => result.Status switch
    {
        SignInStatus.Succeeded => await identity.FindAsync(result.UserId!) is { } user
            ? user
            : AppError.NotFound(AccountErrors.NotFound, "Account not found."),
        SignInStatus.LockedOut => AppError.Forbidden(AccountErrors.LockedOut, "Too many failed attempts. Try again later."),
        // No pending first step (expired or never started): start over.
        SignInStatus.NotAllowed => AppError.Validation(AccountErrors.TwoFactorSessionExpired, "Please sign in again."),
        _ => AppError.Validation(AccountErrors.CodeInvalid, "The code is not correct.")
    };

    private async Task<bool> IsRequiredAsync(string userId)
    {
        if (!security.CurrentValue.RequireStaffTwoFactor)
            return false;
        var user = await identity.FindAsync(userId);
        return user?.IsStaff == true;
    }

    /// <summary>Authenticator codes may be typed with spaces or Persian/Arabic digits.</summary>
    internal static string Clean(string code) =>
        new(PhoneCodeService.NormalizeDigits(code).Where(char.IsAsciiDigit).ToArray());

    /// <summary>"JBSWY3DPEHPK3PXP" → "jbsw y3dp ehpk 3pxp" (easier to type into an app).</summary>
    internal static string FormatKey(string key) =>
        string.Join(' ', key.ToLowerInvariant().Chunk(4).Select(c => new string(c)));

    internal static string AuthenticatorUri(string issuer, string account, string key) =>
        $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(account)}" +
        $"?secret={key}&issuer={Uri.EscapeDataString(issuer)}&digits=6";
}
