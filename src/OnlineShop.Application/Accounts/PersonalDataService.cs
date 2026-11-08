using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OnlineShop.Application.Common;
using OnlineShop.Application.Customers;

namespace OnlineShop.Application.Accounts;

/// <summary>
/// The customer's data rights: download everything stored about them, and delete the account.
/// Deleting erases personal data but keeps orders (financial records) attached to an anonymous account row.
/// </summary>
public sealed class PersonalDataService(
    IAccountIdentity identity,
    IAccountDataIdentity data,
    IApplicationDbContext db,
    AddressService addresses,
    TimeProvider clock,
    IOptions<AuthOptions> auth)
{
    public async Task<Result<PersonalDataExport>> ExportAsync(string userId, CancellationToken cancellationToken = default)
    {
        var account = await data.GetAccountDataAsync(userId);
        if (account is null)
            return AppError.NotFound(AccountErrors.NotFound, "Account not found.");

        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.CustomerId == userId)
            .OrderBy(o => o.OrderedUtc)
            .Select(o => new
            {
                o.Id, o.OrderedUtc, o.Status, o.Subtotal, o.Tax, o.Shipping, o.Total,
                Items = o.Items.Select(i => new OrderItemData(i.ProductName, i.UnitPrice, i.Quantity)).ToList()
            })
            .ToListAsync(cancellationToken);

        var cart = await db.CartItems.AsNoTracking()
            .Where(i => i.Cart.CustomerId == userId)
            .Select(i => new CartItemData(i.Product.Name, i.Quantity))
            .ToListAsync(cancellationToken);

        return new PersonalDataExport(
            clock.GetUtcNow().UtcDateTime,
            account,
            await addresses.ListAsync(userId, cancellationToken),
            orders.Select(o => new OrderData(o.Id, o.OrderedUtc, o.Status.ToString(), o.Subtotal, o.Tax, o.Shipping, o.Total, o.Items)).ToList(),
            cart);
    }

    /// <param name="signedInAt">When this session's sign-in happened (<see cref="AccountClaims.AuthTime"/>), if known.</param>
    public async Task<Result<bool>> DeleteAsync(string userId, DeleteAccountRequest request, DateTimeOffset? signedInAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var user = await identity.FindAsync(userId);
        if (user is null)
            return AppError.NotFound(AccountErrors.NotFound, "Account not found.");

        // Staff accounts are removed by an administrator (staff management), so a shop never loses its last admin.
        if (user.IsStaff)
            return AppError.Forbidden(ProfileErrors.StaffCannotDelete, "Staff accounts cannot delete themselves.");

        var confirmed = await identity.HasPasswordAsync(userId)
            ? !string.IsNullOrEmpty(request.Password) && await data.CheckPasswordAsync(userId, request.Password)
            : signedInAt is { } at && clock.GetUtcNow() - at <= TimeSpan.FromMinutes(auth.Value.ReauthenticationMinutes);
        if (!confirmed)
            return await identity.HasPasswordAsync(userId)
                ? AppError.Validation(ProfileErrors.PasswordIncorrect, "The password is not correct.")
                : AppError.Forbidden(ProfileErrors.ReauthenticationRequired, "Sign in again, then delete the account.");

        // Real deletes (not soft deletes): the point is that the data is gone. Each step is idempotent, and the
        // account row is erased last, so a failure half-way can simply be retried by the still signed-in user.
        await db.CustomerAddresses.IgnoreQueryFilters().Where(a => a.CustomerId == userId).ExecuteDeleteAsync(cancellationToken);
        await db.CartItems.IgnoreQueryFilters().Where(i => i.Cart.CustomerId == userId).ExecuteDeleteAsync(cancellationToken);
        await db.Carts.IgnoreQueryFilters().Where(c => c.CustomerId == userId).ExecuteDeleteAsync(cancellationToken);
        await db.PhoneVerificationCodes
            .Where(c => c.UserId == userId || (user.PhoneNumber != null && c.PhoneNumber == user.PhoneNumber))
            .ExecuteDeleteAsync(cancellationToken);
        // Orders stay: they are financial records and only hold product snapshots and totals. When orders gain
        // shipping/contact snapshots (phase 5), blank them here as well.

        await data.EraseAsync(userId);
        return true;
    }
}
