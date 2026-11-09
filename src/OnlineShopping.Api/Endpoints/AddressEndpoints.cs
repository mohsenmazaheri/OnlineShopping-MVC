using System.Security.Claims;
using OnlineShopping.Api.Common;
using OnlineShopping.Api.Security;
using OnlineShopping.Application.Customers;

namespace OnlineShopping.Api.Endpoints;

/// <summary>The signed-in customer's address book. Ids of other customers' addresses answer 404.</summary>
public static class AddressEndpoints
{
    public static IEndpointRouteBuilder MapAddressEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/account/addresses").WithTags("Account").RequireAuthorization();

        group.MapGet("/", async (ClaimsPrincipal user, AddressService addresses, CancellationToken ct) =>
                TypedResults.Ok(await addresses.ListAsync(user.GetUserId(), ct)))
            .WithName("ListAddresses")
            .WithSummary("The address book, default address first.");

        group.MapPost("/", async (AddressInput input, ClaimsPrincipal user, AddressService addresses, CancellationToken ct) =>
                (await addresses.CreateAsync(user.GetUserId(), input, ct)).ToHttpResult())
            .Validate<AddressInput>()
            .WithName("CreateAddress")
            .WithSummary("Add an address (the first one becomes the default).")
            .Produces<AddressView>();

        group.MapPut("/{id:guid}", async (Guid id, AddressInput input, ClaimsPrincipal user, AddressService addresses, CancellationToken ct) =>
                (await addresses.UpdateAsync(user.GetUserId(), id, input, ct)).ToHttpResult())
            .Validate<AddressInput>()
            .WithName("UpdateAddress")
            .WithSummary("Change an address.")
            .Produces<AddressView>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/default", async (Guid id, ClaimsPrincipal user, AddressService addresses, CancellationToken ct) =>
                (await addresses.SetDefaultAsync(user.GetUserId(), id, ct)).ToHttpResult())
            .WithName("SetDefaultAddress")
            .WithSummary("Make an address the default.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal user, AddressService addresses, CancellationToken ct) =>
                (await addresses.DeleteAsync(user.GetUserId(), id, ct)).ToHttpResult())
            .WithName("DeleteAddress")
            .WithSummary("Delete an address (for real; the next one becomes the default).")
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
