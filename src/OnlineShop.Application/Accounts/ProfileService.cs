using Microsoft.Extensions.Options;
using OnlineShop.Application.Common;

namespace OnlineShop.Application.Accounts;

/// <summary>The signed-in customer edits their name and language, and changes their password.</summary>
public sealed class ProfileService(IAccountIdentity identity, IAccountDataIdentity data, IOptions<StoreOptions> store)
{
    public async Task<Result<CurrentUser>> UpdateAsync(string userId, UpdateProfileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var locale = string.IsNullOrWhiteSpace(request.PreferredLocale) ? null : request.PreferredLocale.Trim();
        if (locale is not null && !store.Value.LocaleList.Contains(locale, StringComparer.Ordinal))
            return AppError.Validation(ProfileErrors.LocaleNotSupported, "This language is not offered by the shop.");

        if (await identity.FindAsync(userId) is null)
            return AppError.NotFound(AccountErrors.NotFound, "Account not found.");

        var name = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim();
        await data.UpdateProfileAsync(userId, name, locale);
        return (await identity.FindAsync(userId))!;
    }

    public async Task<Result<bool>> ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Accounts made by SMS code or a provider have no password to change (they set one with "forgot password").
        if (!await identity.HasPasswordAsync(userId))
            return AppError.Validation(ProfileErrors.NoPassword, "This account signs in without a password.");

        var errors = await data.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
        if (errors.Count == 0)
            return true;
        return errors.Contains("PasswordMismatch")
            ? AppError.Validation(ProfileErrors.PasswordIncorrect, "The current password is not correct.")
            : AppError.Validation(AccountErrors.PasswordRejected, "The password does not meet the requirements.");
    }
}
