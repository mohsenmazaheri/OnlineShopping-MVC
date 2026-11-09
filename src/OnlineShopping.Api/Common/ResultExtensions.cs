using OnlineShopping.Application.Common;

namespace OnlineShopping.Api.Common;

/// <summary>Maps application <see cref="Result{T}"/> values to HTTP responses (RFC 9457 problem details on failure).</summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Match(Results.Ok, ToProblem);
    }

    public static IResult ToProblem(AppError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var status = StatusCodeFor(error.Kind);
        return Results.Problem(
            statusCode: status,
            title: error.Message,
            type: $"https://httpstatuses.io/{status}",
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
    }

    internal static int StatusCodeFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.TooManyRequests => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status500InternalServerError
    };
}
