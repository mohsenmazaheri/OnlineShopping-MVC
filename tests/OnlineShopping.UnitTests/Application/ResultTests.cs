using OnlineShopping.Application.Common;

namespace OnlineShopping.UnitTests.Application;

public sealed class ResultTests
{
    [Fact]
    public void Success_exposes_the_value()
    {
        var result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_exposes_the_error_and_refuses_to_give_a_value()
    {
        var error = AppError.NotFound("thing.not_found", "No thing.");

        var result = Result.Failure<int>(error);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        var ex = Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Contains("thing.not_found", ex.Message);
    }

    [Fact]
    public void Failure_requires_an_error()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Failure<int>(null!));
    }

    [Fact]
    public void Values_and_errors_convert_implicitly()
    {
        Result<string> ok = "hello";
        Result<string> failed = AppError.Conflict("dup", "Duplicate.");

        Assert.True(ok.IsSuccess);
        Assert.False(failed.IsSuccess);
        Assert.Equal(ErrorKind.Conflict, failed.Error!.Kind);
    }

    [Fact]
    public void Match_calls_the_branch_for_the_outcome()
    {
        Assert.Equal("ok:1", Result.Success(1).Match(v => $"ok:{v}", e => $"err:{e.Code}"));
        Assert.Equal("err:x", Result.Failure<int>(AppError.Validation("x", "X")).Match(v => $"ok:{v}", e => $"err:{e.Code}"));
    }

    [Theory]
    [InlineData(ErrorKind.NotFound)]
    [InlineData(ErrorKind.Validation)]
    [InlineData(ErrorKind.Conflict)]
    [InlineData(ErrorKind.Forbidden)]
    public void Error_factories_set_the_kind(ErrorKind kind)
    {
        var error = kind switch
        {
            ErrorKind.NotFound => AppError.NotFound("c", "m"),
            ErrorKind.Validation => AppError.Validation("c", "m"),
            ErrorKind.Conflict => AppError.Conflict("c", "m"),
            _ => AppError.Forbidden("c", "m")
        };

        Assert.Equal(kind, error.Kind);
        Assert.Equal("c", error.Code);
        Assert.Equal("m", error.Message);
    }
}
