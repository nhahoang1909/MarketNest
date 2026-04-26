using MediatR;

namespace MarketNest.Base.Common;

/// <summary>
///     Result monad — the ONLY way to return errors from handlers.
///     Never throw for business failures.
/// </summary>
public class Result<TValue, TError>
{
    private readonly TError? _error;
    private readonly TValue? _value;

    protected Result(TValue value)
    {
        _value = value;
        IsSuccess = true;
    }

    protected Result(TError error)
    {
        _error = error;
        IsSuccess = false;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Result has no value — check IsSuccess first.");

    public TError Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Result has no error — check IsFailure first.");

    public static Result<TValue, TError> Success(TValue value) => new(value);
    public static Result<TValue, TError> Failure(TError error) => new(error);

    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<TError, TResult> onFailure)
        => IsSuccess ? onSuccess(_value!) : onFailure(_error!);

    public Result<TNew, TError> Map<TNew>(Func<TValue, TNew> mapper)
        => IsSuccess
            ? Result<TNew, TError>.Success(mapper(_value!))
            : Result<TNew, TError>.Failure(_error!);

    public async Task<Result<TNew, TError>> MapAsync<TNew>(Func<TValue, Task<TNew>> mapper)
        => IsSuccess
            ? Result<TNew, TError>.Success(await mapper(_value!))
            : Result<TNew, TError>.Failure(_error!);
}

/// <summary>
///     Convenience static factory for Result with Error type.
/// </summary>
public static class Result
{
    public static Result<TValue, Error> Success<TValue>(TValue value)
        => Result<TValue, Error>.Success(value);

    public static Result<TValue, Error> Failure<TValue>(Error error)
        => Result<TValue, Error>.Failure(error);

    public static Result<Unit, Error> Success()
        => Success(Unit.Value);
}
