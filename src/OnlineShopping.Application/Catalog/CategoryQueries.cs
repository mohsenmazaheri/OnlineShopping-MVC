using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OnlineShopping.Application.Common;
using OnlineShopping.Domain.Catalog;

namespace OnlineShopping.Application.Catalog;

public sealed record SubCategorySummary(Guid Id, string Name, string Slug);

public sealed record CategorySummary(Guid Id, string Name, string Slug, IReadOnlyList<SubCategorySummary> SubCategories);

public interface ICategoryQueries
{
    /// <summary>All live categories with their live sub-categories, ordered by name, for menus and the home page.</summary>
    Task<IReadOnlyList<CategorySummary>> GetMenuAsync(CancellationToken cancellationToken = default);

    Task<Result<CategorySummary>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}

public sealed class CategoryQueries(IApplicationDbContext db) : ICategoryQueries
{
    public const string NotFoundCode = "category.not_found";
    public const string SlugRequiredCode = "category.slug_required";

    // Translated to SQL: selects only the needed columns. Soft-deleted rows (also in the
    // SubCategories navigation) are removed by the global query filter.
    private static readonly Expression<Func<Category, CategorySummary>> ToSummary = c => new CategorySummary(
        c.Id,
        c.Name,
        c.Slug,
        c.SubCategories
            .OrderBy(s => s.Name)
            .Select(s => new SubCategorySummary(s.Id, s.Name, s.Slug))
            .ToList());

    public async Task<IReadOnlyList<CategorySummary>> GetMenuAsync(CancellationToken cancellationToken = default) =>
        await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(ToSummary)
            .ToListAsync(cancellationToken);

    public async Task<Result<CategorySummary>> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return AppError.Validation(SlugRequiredCode, "A category slug is required.");

        var category = await db.Categories
            .AsNoTracking()
            .Where(c => c.Slug == slug)
            .Select(ToSummary)
            .SingleOrDefaultAsync(cancellationToken);

        return category is null
            ? AppError.NotFound(NotFoundCode, $"No category with slug '{slug}'.")
            : category;
    }
}
