using Microsoft.EntityFrameworkCore;
using OnlineShopping.Domain.Accounts;
using OnlineShopping.Domain.Catalog;
using OnlineShopping.Domain.Customers;
using OnlineShopping.Domain.Sales;

namespace OnlineShopping.Application.Common;

/// <summary>
/// The data access the application layer needs. Implemented by the Infrastructure <c>AppDbContext</c>;
/// use cases depend on this interface, never on a concrete database provider.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Category> Categories { get; }
    DbSet<SubCategory> SubCategories { get; }
    DbSet<ProductGroup> ProductGroups { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductImage> ProductImages { get; }
    DbSet<Cart> Carts { get; }
    DbSet<CartItem> CartItems { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<PhoneVerificationCode> PhoneVerificationCodes { get; }
    DbSet<CustomerAddress> CustomerAddresses { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
