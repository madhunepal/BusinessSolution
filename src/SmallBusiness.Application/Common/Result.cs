namespace SmallBusiness.Application.Common;

/// <summary>
/// A simple result type for application service operations.
/// Avoids throwing exceptions for expected business rule violations.
/// </summary>
public class Result
{
    public bool Succeeded { get; }
    public string[] Errors { get; }

    protected Result(bool succeeded, IEnumerable<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors.ToArray();
    }

    public static Result Success() => new(true, []);
    public static Result Failure(params string[] errors) => new(false, errors);
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(params string[] errors) => Result<T>.Failure(errors);
}

/// <summary>
/// A result type that carries a value on success.
/// </summary>
public class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool succeeded, T? value, IEnumerable<string> errors)
        : base(succeeded, errors)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, []);
    public new static Result<T> Failure(params string[] errors) => new(false, default, errors);
}
