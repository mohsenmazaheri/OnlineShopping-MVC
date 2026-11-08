using OnlineShop.Application.Catalog;
using OnlineShop.Application.Common;
using OnlineShop.Domain.Catalog;
using OnlineShop.UnitTests.TestSupport;

namespace OnlineShop.UnitTests.Application;

public sealed class CategoryQueriesTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Menu_lists_categories_and_sub_categories_by_name()
    {
        await SeedAsync(
            new Category { Name = "Toys", Slug = "toys" },
            new Category
            {
                Name = "Books",
                Slug = "books",
                SubCategories =
                [
                    new SubCategory { Name = "Poetry", Slug = "poetry" },
                    new SubCategory { Name = "History", Slug = "history" }
                ]
            });

        await using var db = _database.CreateContext();
        var menu = await new CategoryQueries(db).GetMenuAsync();

        Assert.Equal(["Books", "Toys"], menu.Select(c => c.Name));
        Assert.Equal(["History", "Poetry"], menu[0].SubCategories.Select(s => s.Name));
        Assert.Empty(menu[1].SubCategories);
    }

    [Fact]
    public async Task Menu_hides_soft_deleted_categories_and_sub_categories()
    {
        await SeedAsync(
            new Category { Name = "Gone", Slug = "gone", IsDeleted = true },
            new Category
            {
                Name = "Books",
                Slug = "books",
                SubCategories =
                [
                    new SubCategory { Name = "Live", Slug = "live" },
                    new SubCategory { Name = "Deleted", Slug = "deleted", IsDeleted = true }
                ]
            });

        await using var db = _database.CreateContext();
        var menu = await new CategoryQueries(db).GetMenuAsync();

        var books = Assert.Single(menu);
        Assert.Equal(["Live"], books.SubCategories.Select(s => s.Name));
    }

    [Fact]
    public async Task GetBySlug_returns_the_category()
    {
        await SeedAsync(new Category { Name = "Books", Slug = "books" });

        await using var db = _database.CreateContext();
        var result = await new CategoryQueries(db).GetBySlugAsync("books");

        Assert.True(result.IsSuccess);
        Assert.Equal("Books", result.Value.Name);
    }

    [Fact]
    public async Task GetBySlug_reports_not_found_for_unknown_and_deleted_categories()
    {
        await SeedAsync(new Category { Name = "Gone", Slug = "gone", IsDeleted = true });

        await using var db = _database.CreateContext();
        var queries = new CategoryQueries(db);

        foreach (var slug in new[] { "missing", "gone" })
        {
            var result = await queries.GetBySlugAsync(slug);
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorKind.NotFound, result.Error!.Kind);
            Assert.Equal(CategoryQueries.NotFoundCode, result.Error.Code);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetBySlug_rejects_an_empty_slug(string slug)
    {
        await using var db = _database.CreateContext();

        var result = await new CategoryQueries(db).GetBySlugAsync(slug);

        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(CategoryQueries.SlugRequiredCode, result.Error.Code);
    }

    private async Task SeedAsync(params Category[] categories)
    {
        await using var db = _database.CreateContext();
        db.Categories.AddRange(categories);
        await db.SaveChangesAsync();
    }
}
