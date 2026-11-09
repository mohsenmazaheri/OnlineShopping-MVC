using Microsoft.Extensions.Options;
using OnlineShopping.Api.Common;
using OnlineShopping.Application.Accounts;
using OnlineShopping.Application.Catalog;
using OnlineShopping.Application.Common;
using OnlineShopping.Application.Notifications;

namespace OnlineShopping.Api.Endpoints;

/// <summary>Sign-up methods currently available (phone needs a configured SMS provider).</summary>
public sealed record SignUpMethods(bool Email, bool Phone);

/// <summary>Sign-in methods currently available (SMS codes need a configured SMS provider).</summary>
public sealed record SignInMethods(bool Password, bool PhoneCode, IReadOnlyList<string> External);

/// <summary>Checkout rules: whether customers may buy without an account, and where the shop ships (ISO country codes).</summary>
public sealed record CheckoutSettings(bool Guest, IReadOnlyList<string> ShippingCountries);

public sealed record StoreInfoResponse(string Name, string DefaultLocale, IReadOnlyList<string> Locales, SignUpMethods SignUp, SignInMethods SignIn,
    CheckoutSettings Checkout);

/// <summary>Public, read-only storefront endpoints (no authentication).</summary>
public static class StoreEndpoints
{
    public static IEndpointRouteBuilder MapStoreEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1").WithTags("Storefront");

        group.MapGet("/store", (IOptions<StoreOptions> options, IOptions<AuthOptions> auth, ISmsCapability sms, IExternalLoginProviders external) =>
            {
                var store = options.Value;
                var signUp = new SignUpMethods(auth.Value.EmailSignUp, auth.Value.PhoneSignUp && sms.IsEnabled);
                var signIn = new SignInMethods(Password: true, PhoneCode: sms.IsEnabled, External: external.Enabled);
                var checkout = new CheckoutSettings(store.GuestCheckout, store.ShippingCountryList);
                return TypedResults.Ok(new StoreInfoResponse(store.Name, store.DefaultLocale, store.LocaleList, signUp, signIn, checkout));
            })
            .WithName("GetStoreInfo")
            .WithSummary("Store name, the locales it offers, the available sign-up/sign-in methods and checkout settings.");

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
