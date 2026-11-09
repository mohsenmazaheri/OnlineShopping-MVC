using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace OnlineShopping.Infrastructure.Identity;

/// <summary>
/// Replaces Identity's "email is required and unique" rule: an account needs an email or a phone number,
/// and each must be unique when present. (Unique filtered indexes enforce the same in the database.)
/// </summary>
public sealed class AppUserValidator : IUserValidator<AppUser>
{
    public const string EmailOrPhoneRequiredCode = "EmailOrPhoneRequired";
    public const string DuplicateEmailCode = "DuplicateEmail";
    public const string DuplicatePhoneCode = "DuplicatePhoneNumber";

    public async Task<IdentityResult> ValidateAsync(UserManager<AppUser> manager, AppUser user)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);

        // An erased account keeps its row without any contact details on purpose.
        if (user.ErasedUtc is not null)
            return IdentityResult.Success;

        var errors = new List<IdentityError>();
        var email = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email;
        var phone = string.IsNullOrWhiteSpace(user.PhoneNumber) ? null : user.PhoneNumber;

        if (email is null && phone is null)
            errors.Add(new IdentityError { Code = EmailOrPhoneRequiredCode, Description = "An email address or a phone number is required." });

        if (email is not null)
        {
            var owner = await manager.FindByEmailAsync(email);
            if (owner is not null && owner.Id != user.Id)
                errors.Add(new IdentityError { Code = DuplicateEmailCode, Description = "The email address is already in use." });
        }

        if (phone is not null)
        {
            var taken = await manager.Users.AnyAsync(u => u.PhoneNumber == phone && u.Id != user.Id);
            if (taken)
                errors.Add(new IdentityError { Code = DuplicatePhoneCode, Description = "The phone number is already in use." });
        }

        return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed([.. errors]);
    }
}
