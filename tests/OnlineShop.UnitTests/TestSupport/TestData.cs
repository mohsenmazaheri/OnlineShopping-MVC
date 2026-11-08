using OnlineShop.Infrastructure.Persistence;
using OnlineShop.Infrastructure.Persistence.Seeding;
using OnlineShop.Domain.Catalog;
using OnlineShop.Domain.Common;
using OnlineShop.Domain.Sales;
using OnlineShop.Application.Authorization;
using OnlineShop.Infrastructure.Identity;

namespace OnlineShop.UnitTests.TestSupport;

internal static class TestData
{
    public static AppUser User(string email = "user@test.local") =>
        new() { UserName = email, Email = email, NormalizedEmail = email.ToUpperInvariant(), NormalizedUserName = email.ToUpperInvariant() };

    /// <summary>Saves a full Category → SubCategory → ProductGroup → Product chain.</summary>
    public static async Task<Product> SaveProductAsync(AppDbContext db, string sku = "SKU-1", string? slug = null)
    {
        var category = new Category { Name = "Digital", Slug = slug ?? $"digital-{Guid.NewGuid():N}" };
        var sub = new SubCategory { Name = "Phones", Slug = "phones", Category = category };
        var group = new ProductGroup { Name = "Smartphones", SubCategory = sub };
        var product = new Product { Name = "Phone X", SKU = sku, Price = 199.99m, ProductGroup = group };

        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }
}
