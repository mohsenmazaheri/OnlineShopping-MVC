using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OnlineShop.Application.Accounts;
using OnlineShop.Application.Authorization;

namespace OnlineShop.Infrastructure.Identity;

/// <summary><see cref="IAccountIdentity"/> on ASP.NET Core Identity (password hashing, lockout, tokens, cookie).</summary>
public sealed class IdentityAccountService(
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn,
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

    public async Task SignInAsync(string userId, bool rememberMe)
    {
        var user = await users.FindByIdAsync(userId)
                   ?? throw new InvalidOperationException($"User {userId} does not exist.");
        await signIn.SignInAsync(user, rememberMe);
    }

    public Task SignOutAsync() => signIn.SignOutAsync();

    public async Task<CurrentUser?> FindAsync(string userId)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null)
            return null;

        var roles = (await users.GetRolesAsync(user)).Order(StringComparer.Ordinal).ToList();
        var permissions = RolePermissions.For(roles).Order(StringComparer.Ordinal).ToList();
        return new CurrentUser(user.Id, user.DisplayName, user.Email, user.EmailConfirmed, user.PhoneNumber,
            user.PhoneNumberConfirmed, user.PreferredLocale, roles, permissions);
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
}
