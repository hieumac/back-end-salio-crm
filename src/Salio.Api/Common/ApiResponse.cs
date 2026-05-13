namespace Salio.Api.Common;

/// <summary>
/// Format response chuẩn — match với type ApiResponse&lt;T&gt; trong frontend (src/types/common.ts).
/// </summary>
public record ApiResponse<T>(bool Success, T? Data, string? Message = null)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, data, message);
}

public record ApiResponse(bool Success, string? Message = null)
{
    public static ApiResponse Ok(string? message = null) => new(true, message);
    public static ApiResponse Fail(string message) => new(false, message);
}
