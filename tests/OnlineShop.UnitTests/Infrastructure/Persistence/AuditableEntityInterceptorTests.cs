using Microsoft.EntityFrameworkCore;
using OnlineShop.Domain.Catalog;
using OnlineShop.Domain.Common;
using OnlineShop.Domain.Sales;
using OnlineShop.UnitTests.TestSupport;

namespace OnlineShop.UnitTests.Infrastructure.Persistence;

public sealed class AuditableEntityInterceptorTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Insert_sets_CreatedUtc_from_clock_and_leaves_ModifiedUtc_empty()
    {
        await using var db = _database.CreateContext();
        var category = new Category { Name = "Books", Slug = "books" };

        db.Categories.Add(category);
        await db.SaveChangesAsync();

        Assert.Equal(_database.Clock.GetUtcNow().UtcDateTime, category.CreatedUtc);
        Assert.Null(category.ModifiedUtc);
    }

    [Fact]
    public async Task Update_sets_ModifiedUtc_and_keeps_original_CreatedUtc()
    {
        var category = await SaveCategoryAsync();
        var created = category.CreatedUtc;
        _database.Clock.Advance(TimeSpan.FromHours(2));

        await using (var db = _database.CreateContext())
        {
            var loaded = await db.Categories.SingleAsync(c => c.Id == category.Id);
            loaded.Name = "Renamed";
            loaded.CreatedUtc = DateTime.MinValue; // an update must not be able to rewrite the creation time
            await db.SaveChangesAsync();
        }

        await using var verify = _database.CreateContext();
        var saved = await verify.Categories.SingleAsync(c => c.Id == category.Id);
        Assert.Equal("Renamed", saved.Name);
        Assert.Equal(created, saved.CreatedUtc);
        Assert.Equal(_database.Clock.GetUtcNow().UtcDateTime, saved.ModifiedUtc);
    }

    [Fact]
    public async Task Remove_soft_deletes_the_row_instead_of_deleting_it()
    {
        var category = await SaveCategoryAsync();
        _database.Clock.Advance(TimeSpan.FromMinutes(5));

        await using (var db = _database.CreateContext())
        {
            db.Categories.Remove(await db.Categories.SingleAsync(c => c.Id == category.Id));
            await db.SaveChangesAsync();
        }

        await using var verify = _database.CreateContext();
        var row = await verify.Categories.IgnoreQueryFilters().SingleAsync(c => c.Id == category.Id);
        Assert.True(row.IsDeleted);
        Assert.Equal(_database.Clock.GetUtcNow().UtcDateTime, row.ModifiedUtc);
        Assert.Equal(category.CreatedUtc, row.CreatedUtc);
    }

    [Fact]
    public async Task Remove_soft_deletes_tracked_cascade_dependents_too()
    {
        Guid categoryId, subCategoryId;
        await using (var db = _database.CreateContext())
        {
            var category = new Category { Name = "Toys", Slug = "toys" };
            var sub = new SubCategory { Name = "Puzzles", Slug = "puzzles", Category = category };
            db.SubCategories.Add(sub);
            await db.SaveChangesAsync();
            (categoryId, subCategoryId) = (category.Id, sub.Id);
        }

        await using (var db = _database.CreateContext())
        {
            var category = await db.Categories.Include(c => c.SubCategories).SingleAsync(c => c.Id == categoryId);
            db.Categories.Remove(category);
            await db.SaveChangesAsync();
        }

        await using var verify = _database.CreateContext();
        var sub2 = await verify.SubCategories.IgnoreQueryFilters().SingleAsync(s => s.Id == subCategoryId);
        Assert.True(sub2.IsDeleted);
    }

    private async Task<Category> SaveCategoryAsync()
    {
        await using var db = _database.CreateContext();
        var category = new Category { Name = "Books", Slug = $"books-{Guid.NewGuid():N}" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }
}
