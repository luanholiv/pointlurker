namespace PointLurker.Core.Application;

public readonly record struct Result<TValue, TError>
{
    private readonly TValue? _value;
    private readonly TError? _error;
    private readonly bool _isSuccess;

    private Result(TValue value)
    {
        _value = value;
        _error = default;
        _isSuccess = true;
    }

    private Result(TError error)
    {
        _value = default;
        _error = error;
        _isSuccess = false;
    }

    public bool IsSuccess => _isSuccess;
    public bool IsFailure => !_isSuccess;

    public TValue Value =>
        _isSuccess
            ? _value!
            : throw new InvalidOperationException("Cannot access Value of a failed result");

    public TError Error =>
        !_isSuccess
            ? _error!
            : throw new InvalidOperationException("Cannot access Error of a successful result");

    public static Result<TValue, TError> Success(TValue value) => new(value);

    public static Result<TValue, TError> Failure(TError error) => new(error);

    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<TError, TResult> onFailure
    ) => _isSuccess ? onSuccess(_value!) : onFailure(_error!);
}

public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
