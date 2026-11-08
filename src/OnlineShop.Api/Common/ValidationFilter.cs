using FluentValidation;

namespace OnlineShop.Api.Common;

/// <summary>
/// Runs the FluentValidation validator for a request body before the endpoint. Failures become a 400
/// validation problem whose <c>errors</c> map field → error codes (the web app translates the codes).
/// </summary>
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var request = context.Arguments.OfType<T>().FirstOrDefault();
        if (request is null)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "A request body is required.");

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (result.IsValid)
            return await next(context);

        var errors = result.Errors
            .GroupBy(e => JsonFieldName(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorCode).Distinct().ToArray());
        return Results.ValidationProblem(errors, title: "The request is not valid.");
    }

    /// <summary>"NewPassword" → "newPassword", matching the JSON property names clients send.</summary>
    internal static string JsonFieldName(string propertyName) =>
        string.IsNullOrEmpty(propertyName) ? propertyName : char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
}

public static class ValidationFilterExtensions
{
    public static RouteHandlerBuilder Validate<T>(this RouteHandlerBuilder builder) where T : class =>
        builder.AddEndpointFilter<ValidationFilter<T>>().ProducesValidationProblem();
}
