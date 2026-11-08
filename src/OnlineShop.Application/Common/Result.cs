namespace OnlineShop.Application.Common;

/// <summary>Kind of business failure; the API maps each kind to one HTTP status code.</summary>
public enum ErrorKind
{
    Validation,
    NotFound,
    Conflict,
    Forbidden
}

/// <summary>A business error: a stable machine-readable <see cref="Code"/> plus a human-readable message.</summary>
public sealed record AppError(ErrorKind Kind, string Code, string Message)
{
    public static AppError NotFound(string code, string message) => new(ErrorKind.NotFound, code, message);
    public static AppError Validation(string code, string message) => new(ErrorKind.Validation, code, message);
    public static AppError Conflict(string code, string message) => new(ErrorKind.Conflict, code, message);
    public static AppError Forbidden(string code, string message) => new(ErrorKind.Forbidden, code, message);
}

/// <summary>Factory methods for <see cref="Result{T}"/>.</summary>
public static class Result
{
    public static Result<T> Success<T>(T value) => new(value);

    public static Result<T> Failure<T>(AppError error) => new(error ?? throw new ArgumentNullException(nameof(error)));
}

/// <summary>
/// Outcome of a use case. Expected business failures are returned as <see cref="AppError"/> values instead of
/// thrown exceptions; exceptions are reserved for bugs and infrastructure failures.
/// </summary>
public sealed class Result<T>
{
    private readonly T? _value;

    internal Result(T value)
    {
        _value = value;
        IsSuccess = true;
    }

    internal Result(AppError error)
    {
        Error = error;
    }

    public bool IsSuccess { get; }

    public AppError? Error { get; }

    /// <summary>The value of a successful result; reading it from a failure is a programming error.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read the value of a failed result ({Error!.Code}).");

#pragma warning disable CA2225 // Named alternates exist: Result.Success / Result.Failure.
    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(AppError error) => Result.Failure<T>(error);
#pragma warning restore CA2225

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<AppError, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);
        return IsSuccess ? onSuccess(_value!) : onFailure(Error!);
    }
}
