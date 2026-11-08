using Microsoft.Extensions.Options;
using OnlineShop.Api.Common;
using OnlineShop.Application.Catalog;
using OnlineShop.Application.Common;

namespace OnlineShop.Api.Endpoints;

public sealed record StoreInfoResponse(string Name, string DefaultLocale, IReadOnlyList<string> Locales);

/// <summary>Public, read-only storefront endpoints (no authentication).</summary>
public static class StoreEndpoints
{
    public static IEndpointRouteBuilder MapStoreEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Storefront");

        group.MapGet("/store", (IOptions<StoreOptions> options) =>
            {
                var store = options.Value;
                return TypedResults.Ok(new StoreInfoResponse(store.Name, store.DefaultLocale, store.LocaleList));
            })
            .WithName("GetStoreInfo")
            .WithSummary("Store name and the locales it offers.");

        group.MapGet("/catalog/categories", async (ICategoryQueries queries, CancellationToken ct) =>
                TypedResults.Ok(await queries.GetMenuAsync(ct)))
            .WithName("GetCategoryMenu")
            .WithSummary("All categories with their sub-categories, for menus and the home page.");

        group.MapGet("/catalog/categories/{slug}", async (string slug, ICategoryQueries queries, CancellationToken ct) =>
                (await queries.GetBySlugAsync(slug, ct)).ToHttpResult())
            .WithName("GetCategoryBySlug")
            .WithSummary("One category by its slug.")
            .Produces<CategorySummary>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
