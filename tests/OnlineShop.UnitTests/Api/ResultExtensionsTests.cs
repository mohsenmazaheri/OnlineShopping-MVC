using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using OnlineShop.Api.Common;
using OnlineShop.Application.Common;

namespace OnlineShop.UnitTests.Api;

public sealed class ResultExtensionsTests
{
    [Theory]
    [InlineData(ErrorKind.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorKind.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorKind.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorKind.Forbidden, StatusCodes.Status403Forbidden)]
    public void Each_error_kind_maps_to_its_status_code(ErrorKind kind, int status)
    {
        Assert.Equal(status, ResultExtensions.StatusCodeFor(kind));
    }

    [Fact]
    public void Success_becomes_200_with_the_value()
    {
        var http = Result.Success("value").ToHttpResult();

        var ok = Assert.IsType<Ok<string>>(http);
        Assert.Equal("value", ok.Value);
    }

    [Fact]
    public void Failure_becomes_problem_details_carrying_the_error_code()
    {
        var http = Result.Failure<string>(AppError.NotFound("category.not_found", "No category.")).ToHttpResult();

        var problem = Assert.IsType<ProblemHttpResult>(http);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
        Assert.Equal("No category.", problem.ProblemDetails.Title);
        Assert.Equal("category.not_found", problem.ProblemDetails.Extensions["code"]);
    }
}
