using System.Text.Json.Serialization;

namespace Salio.Api.Common;

/// <summary>
/// Format response chuẩn của mọi endpoint API.
/// <code>
/// {
///   "status": "success",
///   "code": 200,
///   "message": "Lấy thông tin người dùng thành công",
///   "data": { ... }
/// }
/// </code>
/// </summary>
public record ApiResponse
{
    /// <summary>"success" khi thành công, "error"/"fail" khi lỗi.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = ApiStatus.Success;

    /// <summary>HTTP status code (200, 201, 400, 401, 404, 422, 500…).</summary>
    [JsonPropertyName("code")]
    public int Code { get; init; } = 200;

    /// <summary>Mô tả ngắn cho client / hiển thị toast.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>Payload trả về cho client (object, list, primitive… hoặc null khi lỗi).</summary>
    [JsonPropertyName("data")]
    public object? Data { get; init; }

    /// <summary>Chi tiết lỗi (validation errors, error code symbolic…). Bỏ qua khi null.</summary>
    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Errors { get; init; }

    /// <summary>TraceId của request — phục vụ debug / log. Bỏ qua khi null.</summary>
    [JsonPropertyName("traceId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TraceId { get; init; }

    /// <summary>Tạo response thành công không có data (DELETE, UPDATE, action lệnh…).</summary>
    public static ApiResponse Ok(string? message = null, int code = 200) => new()
    {
        Status = ApiStatus.Success,
        Code = code,
        Message = message,
    };

    /// <summary>Tạo response lỗi.</summary>
    public static ApiResponse Fail(string message, int code = 400, object? errors = null, string? traceId = null) => new()
    {
        Status = ApiStatus.Error,
        Code = code,
        Message = message,
        Errors = errors,
        TraceId = traceId,
    };
}

/// <summary>Response thành công có data kiểu T (dùng cho [ProducesResponseType]).</summary>
public record ApiResponse<T>
{
    [JsonPropertyName("status")]
    public string Status { get; init; } = ApiStatus.Success;

    [JsonPropertyName("code")]
    public int Code { get; init; } = 200;

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("data")]
    public T? Data { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null, int code = 200) => new()
    {
        Status = ApiStatus.Success,
        Code = code,
        Message = message,
        Data = data,
    };
}

/// <summary>Giá trị chuẩn cho trường <c>status</c>.</summary>
public static class ApiStatus
{
    public const string Success = "success";
    public const string Error = "error";
    public const string Fail = "fail";
}
