namespace SmartBank.BuildingBlocks.Domain;

public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);
}

public enum ErrorType
{
    Validation = 0,
    NotFound = 1,
    Conflict = 2,
    Forbidden = 3,
    Unauthorized = 4,
    Unexpected = 5
}

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, new Error("OK", "OK", ErrorType.Unexpected));
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, new Error("OK", "OK", ErrorType.Unexpected));
    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}

public class Result<T> : Result
{
    public T? Value { get; }

    internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error)
    {
        Value = value;
    }
}
