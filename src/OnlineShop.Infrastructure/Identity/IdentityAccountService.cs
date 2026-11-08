using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OnlineShop.Application.Accounts;
using OnlineShop.Application.Authorization;

namespace OnlineShop.Infrastructure.Identity;

/// <summary><see cref="IAccountIdentity"/> on ASP.NET Core Identity (password hashing, lockout, tokens, cookie).</summary>
public sealed class IdentityAccountService(
    UserManager<AppUser> users,
    AppSignInManager signIn,
    TimeProvider clock) : IAccountIdentity
{
    public async Task<bool> EmailInUseAsync(string email, CancellationToken cancellationToken = default) =>
        await users.FindByEmailAsync(email) is not null;

    public Task<bool> PhoneInUseAsync(string phoneE164, CancellationToken cancellationToken = default) =>
        users.Users.AnyAsync(u => u.PhoneNumber == phoneE164, cancellationToken);

    public async Task<IdentityCreateResult> CreateCustomerAsync(
        string? email, string? phoneE164, string password, string? displayName, string? locale,
        CancellationToken cancellationToken = default)
    {
        var user = new AppUser
        {
            // Internal, random and stable: users never see or type it.
            UserName = Guid.CreateVersion7().ToString("N"),
            Email = email,
            PhoneNumber = phoneE164,
            DisplayName = displayName,
            PreferredLocale = locale,
            CreatedUtc = clock.GetUtcNow().UtcDateTime,
            LockoutEnabled = true
        };

        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
            return IdentityCreateResult.Failed(created.Errors.Select(e => e.Code).ToList());

        var role = await users.AddToRoleAsync(user, AppRoles.Customer);
        if (!role.Succeeded)
            throw new InvalidOperationException("Could not add the new user to the Customer role: " +
                                                string.Join("; ", role.Errors.Select(e => e.Code)));

        return IdentityCreateResult.Success(user.Id);
    }

    public async Task<(SignInStatus Status, string? UserId)> PasswordSignInAsync(LoginIdentifier identifier, string password, bool rememberMe)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        var user = identifier.Email is not null
            ? await users.FindByEmailAsync(identifier.Email)
            : await users.Users.SingleOrDefaultAsync(u => u.PhoneNumber == identifier.PhoneE164);

        if (user is null)
        {
            // Spend roughly the time of a real password check so response timing does not reveal unknown accounts.
            users.PasswordHasher.HashPassword(new AppUser(), password);
            return (SignInStatus.InvalidCredentials, null);
        }

        var result = await signIn.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure: true);
        var status = result switch
        {
            { Succeeded: true } => SignInStatus.Succeeded,
            { IsLockedOut: true } => SignInStatus.LockedOut,
            { RequiresTwoFactor: true } => SignInStatus.TwoFactorRequired,
            { IsNotAllowed: true } => SignInStatus.NotAllowed,
            _ => SignInStatus.InvalidCredentials
        };
        return (status, status == SignInStatus.Succeeded ? user.Id : null);
    }

    public async Task<SignInStatus> SignInAsync(string userId, bool rememberMe)
    {
        var user = await users.FindByIdAsync(userId)
                   ?? throw new InvalidOperationException($"User {userId} does not exist.");
        var result = await signIn.SignInWithSecondStepAsync(user, rememberMe);
        return result.RequiresTwoFactor ? SignInStatus.TwoFactorRequired : SignInStatus.Succeeded;
    }

    public Task SignOutAsync() => signIn.SignOutAsync();

    public async Task<CurrentUser?> FindAsync(string userId)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null || user.ErasedUtc is not null)
            return null;

        var roles = (await users.GetRolesAsync(user)).Order(StringComparer.Ordinal).ToList();
        var permissions = RolePermissions.For(roles).Order(StringComparer.Ordinal).ToList();
        return new CurrentUser(user.Id, user.DisplayName, user.Email, user.EmailConfirmed, user.PhoneNumber,
            user.PhoneNumberConfirmed, user.PreferredLocale, roles, permissions, user.TwoFactorEnabled, user.PasswordHash is not null);
    }

    public async Task<string?> CreateEmailConfirmationTokenAsync(string userId)
    {
        var user = await users.FindByIdAsync(userId);
        return user?.Email is null ? null : await users.GenerateEmailConfirmationTokenAsync(user);
    }

    public async Task<bool> ConfirmEmailAsync(string userId, string token)
    {
        var user = await users.FindByIdAsync(userId);
        return user is not null && (await users.ConfirmEmailAsync(user, token)).Succeeded;
    }

    public async Task<(string UserId, string Token, string? Locale)?> CreatePasswordResetTokenAsync(string email)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
            return null;
        return (user.Id, await users.GeneratePasswordResetTokenAsync(user), user.PreferredLocale);
    }

    public async Task<IdentityCreateResult> ResetPasswordAsync(string userId, string token, string newPassword)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return IdentityCreateResult.Failed(["InvalidToken"]);

        var result = await users.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
            return IdentityCreateResult.Failed(result.Errors.Select(e => e.Code).ToList());

        // A reset ends every other session and clears any lockout.
        await users.UpdateSecurityStampAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return IdentityCreateResult.Success(user.Id);
    }

    public Task<string?> FindUserIdByPhoneAsync(string phoneE164, CancellationToken cancellationToken = default) =>
        users.Users.Where(u => u.PhoneNumber == phoneE164).Select(u => u.Id).SingleOrDefaultAsync(cancellationToken);

    public async Task<IdentityCreateResult> CreatePhoneCustomerAsync(string phoneE164, string? displayName, string? locale,
        CancellationToken cancellationToken = default)
    {
        var user = new AppUser
        {
            UserName = Guid.CreateVersion7().ToString("N"),
            PhoneNumber = phoneE164,
            // Created right after the code was entered: the number is proven.
            PhoneNumberConfirmed = true,
            DisplayName = displayName,
            PreferredLocale = locale,
            CreatedUtc = clock.GetUtcNow().UtcDateTime,
            LockoutEnabled = true
        };

        var created = await users.CreateAsync(user);
        if (!created.Succeeded)
            return IdentityCreateResult.Failed(created.Errors.Select(e => e.Code).ToList());

        var role = await users.AddToRoleAsync(user, AppRoles.Customer);
        if (!role.Succeeded)
            throw new InvalidOperationException("Could not add the new user to the Customer role: " +
                                                string.Join("; ", role.Errors.Select(e => e.Code)));
        return IdentityCreateResult.Success(user.Id);
    }

    public async Task<bool> IsLockedOutAsync(string userId)
    {
        var user = await users.FindByIdAsync(userId);
        return user is not null && await users.IsLockedOutAsync(user);
    }

    public async Task<string?> FindUserIdByEmailAsync(string email) => (await users.FindByEmailAsync(email))?.Id;

    public async Task<string?> FindUserIdByExternalLoginAsync(string provider, string providerKey) =>
        (await users.FindByLoginAsync(ExternalSchemes.For(provider), providerKey))?.Id;

    public async Task<IdentityCreateResult> CreateExternalCustomerAsync(string email, bool emailConfirmed, string? displayName, string? locale)
    {
        var user = new AppUser
        {
            UserName = Guid.CreateVersion7().ToString("N"),
            Email = email,
            EmailConfirmed = emailConfirmed,
            DisplayName = displayName,
            PreferredLocale = locale,
            CreatedUtc = clock.GetUtcNow().UtcDateTime,
            LockoutEnabled = true
        };

        var created = await users.CreateAsync(user);
        if (!created.Succeeded)
            return IdentityCreateResult.Failed(created.Errors.Select(e => e.Code).ToList());

        var role = await users.AddToRoleAsync(user, AppRoles.Customer);
        if (!role.Succeeded)
            throw new InvalidOperationException("Could not add the new user to the Customer role: " +
                                                string.Join("; ", role.Errors.Select(e => e.Code)));
        return IdentityCreateResult.Success(user.Id);
    }

    public async Task<IdentityCreateResult> AddExternalLoginAsync(string userId, string provider, string providerKey, string? displayName)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return IdentityCreateResult.Failed(["NotFound"]);

        var scheme = ExternalSchemes.For(provider);
        var result = await users.AddLoginAsync(user, new UserLoginInfo(scheme, providerKey, displayName ?? scheme));
        return result.Succeeded
            ? IdentityCreateResult.Success(user.Id)
            : IdentityCreateResult.Failed(result.Errors.Select(e => e.Code).ToList());
    }

    public async Task<IReadOnlyList<string>> GetExternalLoginsAsync(string userId)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return [];
        return (await users.GetLoginsAsync(user))
            .Select(l => ExternalSchemes.ProviderForScheme(l.LoginProvider))
            .OfType<string>()
            .Distinct()
            .ToList();
    }

    public async Task<IdentityCreateResult> RemoveExternalLoginAsync(string userId, string provider)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return IdentityCreateResult.Failed(["NotFound"]);

        var scheme = ExternalSchemes.For(provider);
        foreach (var login in (await users.GetLoginsAsync(user)).Where(l => l.LoginProvider == scheme))
        {
            var result = await users.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);
            if (!result.Succeeded)
                return IdentityCreateResult.Failed(result.Errors.Select(e => e.Code).ToList());
        }
        return IdentityCreateResult.Success(user.Id);
    }

    public async Task<bool> HasPasswordAsync(string userId)
    {
        var user = await users.FindByIdAsync(userId);
        return user is not null && await users.HasPasswordAsync(user);
    }

    public async Task<IdentityCreateResult> SetConfirmedPhoneAsync(string userId, string phoneE164)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return IdentityCreateResult.Failed(["NotFound"]);

        user.PhoneNumber = phoneE164;
        user.PhoneNumberConfirmed = true;
        // UpdateAsync runs AppUserValidator, which rejects a number another account already uses.
        var result = await users.UpdateAsync(user);
        return result.Succeeded
            ? IdentityCreateResult.Success(user.Id)
            : IdentityCreateResult.Failed(result.Errors.Select(e => e.Code).ToList());
    }
}
