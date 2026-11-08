using Microsoft.EntityFrameworkCore;
using OnlineShop.UnitTests.TestSupport;

namespace OnlineShop.UnitTests.Infrastructure.Persistence;

public sealed class NamingConventionTests : IDisposable
{
    private readonly SqliteTestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public void Every_table_and_column_name_is_lower_snake_case()
    {
        using var db = _database.CreateContext();

        var names = db.Model.GetEntityTypes()
            .SelectMany(t => new[] { t.GetTableName()! }
                .Concat(t.GetProperties().Select(p => p.GetColumnName())))
            .Distinct()
            .ToList();

        Assert.NotEmpty(names);
        Assert.All(names, n => Assert.Matches("^[a-z][a-z0-9_]*$", n));
    }

    [Theory]
    [InlineData(typeof(OnlineShop.Infrastructure.Identity.AppUser), "users")]
    [InlineData(typeof(Microsoft.AspNetCore.Identity.IdentityRole), "roles")]
    public void Identity_tables_have_short_snake_case_names(Type entity, string table)
    {
        using var db = _database.CreateContext();

        Assert.Equal(table, db.Model.FindEntityType(entity)!.GetTableName());
    }
}
