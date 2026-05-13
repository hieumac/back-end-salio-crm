namespace Salio.Domain.Common;

/// <summary>
/// Result object — gói gọn success/failure cho domain & application layer.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public string? Code { get; }

    protected Result(bool isSuccess, string? error, string? code)
    {
        IsSuccess = isSuccess;
        Error = error;
        Code = code;
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string error, string? code = null) => new(false, error, code);
}

public class Result<T> : Result
{
    public T? Value { get; }

    private Result(T? value, bool isSuccess, string? error, string? code)
        : base(isSuccess, error, code) => Value = value;

    public static Result<T> Success(T value) => new(value, true, null, null);
    public new static Result<T> Failure(string error, string? code = null) => new(default, false, error, code);
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long Total { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}
