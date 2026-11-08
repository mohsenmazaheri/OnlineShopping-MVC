using Microsoft.EntityFrameworkCore;
using OnlineShop.Domain.Catalog;
using OnlineShop.Domain.Sales;

namespace OnlineShop.Application.Common;

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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
