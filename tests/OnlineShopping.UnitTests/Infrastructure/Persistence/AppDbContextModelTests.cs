using Microsoft.EntityFrameworkCore;
using OnlineShopping.Infrastructure.Persistence;
using OnlineShopping.Infrastructure.Persistence.Seeding;
using OnlineShopping.Domain.Catalog;
using OnlineShopping.Domain.Common;
using OnlineShopping.Domain.Sales;
using OnlineShopping.Application.Authorization;
using OnlineShopping.Infrastructure.Identity;
using OnlineShopping.UnitTests.TestSupport;

namespace OnlineShopping.UnitTests.Infrastructure.Persistence;

public sealed class AppDbContextModelTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public void Every_BaseEntity_type_has_a_soft_delete_query_filter()
    {
        using var db = _database.CreateContext();

        var entityTypes = db.Model.GetEntityTypes().Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)).ToList();

        Assert.NotEmpty(entityTypes);
        Assert.All(entityTypes, t => Assert.NotEmpty(t.GetDeclaredQueryFilters()));
    }

    [Fact]
    public void Every_decimal_property_has_money_precision()
    {
        using var db = _database.CreateContext();

        var decimals = db.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => (Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType) == typeof(decimal))
            .ToList();

        Assert.NotEmpty(decimals);
        Assert.All(decimals, p =>
        {
            Assert.Equal(18, p.GetPrecision());
            Assert.Equal(2, p.GetScale());
        });
    }

    [Theory]
    [InlineData(typeof(Cart), nameof(Cart.CustomerId))]
    [InlineData(typeof(Order), nameof(Order.CustomerId))]
    public void User_id_columns_are_foreign_keys_to_AspNetUsers(Type entity, string property)
    {
        using var db = _database.CreateContext();

        var fk = db.Model.FindEntityType(entity)!.GetForeignKeys()
            .SingleOrDefault(f => f.Properties.Single().Name == property);

        Assert.NotNull(fk);
        Assert.Equal(typeof(AppUser), fk.PrincipalEntityType.ClrType);
    }

    [Fact]
    public void Deleting_a_user_never_deletes_their_orders()
    {
        using var db = _database.CreateContext();

        var fk = db.Model.FindEntityType(typeof(Order))!.GetForeignKeys()
            .Single(f => f.PrincipalEntityType.ClrType == typeof(AppUser));

        Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
    }

    [Fact]
    public void Order_status_check_constraint_lists_every_enum_value()
    {
        var sql = AppDbContext.BuildOrderStatusCheckSql();

        Assert.All(Enum.GetNames<OrderStatus>(), name => Assert.Contains($"'{name}'", sql));
    }

    [Fact]
    public async Task Soft_deleted_rows_are_hidden_from_queries_unless_filters_are_ignored()
    {
        await using (var db = _database.CreateContext())
        {
            db.Categories.AddRange(
                new Category { Name = "Visible", Slug = "visible" },
                new Category { Name = "Deleted", Slug = "deleted", IsDeleted = true });
            await db.SaveChangesAsync();
        }

        await using var verify = _database.CreateContext();
        Assert.Equal(["visible"], await verify.Categories.Select(c => c.Slug).ToListAsync());
        Assert.Equal(2, await verify.Categories.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Sku_must_be_unique_among_live_products()
    {
        await using (var db = _database.CreateContext())
            await TestData.SaveProductAsync(db, sku: "DUP");

        await using var db2 = _database.CreateContext();
        await Assert.ThrowsAsync<DbUpdateException>(() => TestData.SaveProductAsync(db2, sku: "DUP"));
    }

    [Fact]
    public async Task Sku_of_a_soft_deleted_product_can_be_reused()
    {
        await using (var db = _database.CreateContext())
        {
            var old = await TestData.SaveProductAsync(db, sku: "REUSE");
            db.Products.Remove(old);
            await db.SaveChangesAsync();
        }

        await using var db2 = _database.CreateContext();
        var reused = await TestData.SaveProductAsync(db2, sku: "REUSE");

        Assert.Equal(2, await db2.Products.IgnoreQueryFilters().CountAsync(p => p.SKU == "REUSE"));
        Assert.Equal(reused.Id, (await db2.Products.SingleAsync(p => p.SKU == "REUSE")).Id);
    }

    [Fact]
    public async Task Order_status_is_stored_as_text()
    {
        await using var db = _database.CreateContext();
        var customer = TestData.User();
        db.Users.Add(customer);
        db.Orders.Add(new Order { CustomerId = customer.Id, Status = OrderStatus.Shipped });
        await db.SaveChangesAsync();

        var stored = await db.Database.SqlQueryRaw<string>("SELECT status AS \"Value\" FROM orders").SingleAsync();

        Assert.Equal("Shipped", stored);
    }

    [Fact]
    public async Task An_order_cannot_reference_a_missing_user()
    {
        await using var db = _database.CreateContext();
        db.Orders.Add(new Order { CustomerId = "no-such-user" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
