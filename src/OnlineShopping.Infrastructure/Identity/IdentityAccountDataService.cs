using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OnlineShopping.Application.Accounts;
using OnlineShopping.Infrastructure.Persistence;

namespace OnlineShopping.Infrastructure.Identity;

/// <summary><see cref="IAccountDataIdentity"/> on ASP.NET Core Identity and the application database.</summary>
public sealed class IdentityAccountDataService(
    UserManager<AppUser> users,
    AppSignInManager signIn,
    AppDbContext db,
    TimeProvider clock) : IAccountDataIdentity
{
    public async Task UpdateProfileAsync(string userId, string? displayName, string? preferredLocale)
    {
        var user = await RequireAsync(userId);
        user.DisplayName = displayName;
        user.PreferredLocale = preferredLocale;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException("Could not update the profile: " + string.Join("; ", result.Errors.Select(e => e.Code)));
    }

    public async Task<IReadOnlyList<string>> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await RequireAsync(userId);
        var result = await users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
            return result.Errors.Select(e => e.Code).ToList();

        // The new security stamp ends every other session; this one gets a fresh cookie and stays signed in.
        await signIn.RefreshSignInAsync(user);
        return [];
    }

    public async Task<bool> CheckPasswordAsync(string userId, string password)
    {
        var user = await RequireAsync(userId);
        return (await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true)).Succeeded;
    }

    public async Task<AccountData?> GetAccountDataAsync(string userId)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null || user.ErasedUtc is not null)
            return null;

        var logins = (await users.GetLoginsAsync(user)).Select(l => l.LoginProvider).Order(StringComparer.Ordinal).ToList();
        var roles = (await users.GetRolesAsync(user)).Order(StringComparer.Ordinal).ToList();
        return new AccountData(user.Id, user.DisplayName, user.Email, user.EmailConfirmed, user.PhoneNumber, user.PhoneNumberConfirmed,
            user.PreferredLocale, user.CreatedUtc, user.TwoFactorEnabled, logins, roles);
    }

    public async Task EraseAsync(string userId)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId);
        if (user is null || user.ErasedUtc is not null)
            return;

        // Sign-in methods and everything hanging off the account go first.
        await db.UserLogins.Where(l => l.UserId == userId).ExecuteDeleteAsync();
        await db.UserTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync();
        await db.UserClaims.Where(c => c.UserId == userId).ExecuteDeleteAsync();
        await db.UserRoles.Where(r => r.UserId == userId).ExecuteDeleteAsync();

        // The row stays (orders point at it) but says nothing about the person and can never sign in again.
        user.UserName = "erased-" + user.Id;
        user.NormalizedUserName = user.UserName.ToUpperInvariant();
        user.Email = user.NormalizedEmail = null;
        user.EmailConfirmed = false;
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.DisplayName = null;
        user.PreferredLocale = null;
        user.PasswordHash = null;
        user.TwoFactorEnabled = false;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.AccessFailedCount = 0;
        user.ErasedUtc = clock.GetUtcNow().UtcDateTime;
        // A new stamp invalidates every session cookie at the next check.
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ConcurrencyStamp = Guid.NewGuid().ToString();
        await db.SaveChangesAsync();

        await signIn.SignOutAsync();
    }

    private async Task<AppUser> RequireAsync(string userId) =>
        await users.FindByIdAsync(userId) is { ErasedUtc: null } user
            ? user
            : throw new InvalidOperationException($"User {userId} does not exist.");
}
