using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Salio.Api.Common;

/// <summary>
/// Filter global — tự động bọc mọi response chưa được wrap vào <see cref="ApiResponse"/>
/// theo format { status, code, message, data }.
/// </summary>
/// <remarks>
/// Bỏ qua nếu giá trị đã là <see cref="ApiResponse"/> / <see cref="ApiResponse{T}"/> (controller đã wrap).
/// Bọc cả <see cref="ProblemDetails"/> / <see cref="ValidationProblemDetails"/> thành định dạng chung.
/// </remarks>
public sealed class ResponseWrapperFilter : IAsyncResultFilter
{
    public Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        switch (context.Result)
        {
            // ─── ObjectResult: trả về object/DTO trực tiếp ───
            case ObjectResult obj:
                context.Result = WrapObjectResult(obj, context.HttpContext.TraceIdentifier);
                break;

            // ─── NoContent / Ok() rỗng / 2xx-status không body ───
            case StatusCodeResult statusResult:
                context.Result = WrapStatusCodeResult(statusResult.StatusCode);
                break;

            case EmptyResult:
                context.Result = WrapStatusCodeResult(StatusCodes.Status200OK);
                break;
        }

        return next();
    }

    // ──────────────────────────────────────────────────────────────────────────
    private static ObjectResult WrapObjectResult(ObjectResult obj, string traceId)
    {
        var statusCode = obj.StatusCode ?? StatusCodes.Status200OK;
        var value = obj.Value;

        // 1. Đã được wrap → giữ nguyên
        if (value is ApiResponse || (value is not null && IsGenericApiResponse(value.GetType())))
        {
            return obj;
        }

        // 2. ProblemDetails / ValidationProblemDetails → wrap thành ApiResponse error
        if (value is ProblemDetails pd)
        {
            var code = obj.StatusCode ?? pd.Status ?? StatusCodes.Status400BadRequest;
            object? errors = pd is ValidationProblemDetails vpd
                ? vpd.Errors.Select(kv => new { field = kv.Key, errors = kv.Value })
                : null;

            return new ObjectResult(new ApiResponse
            {
                Status = ApiStatus.Error,
                Code = code,
                Message = pd.Title ?? pd.Detail ?? "Request invalid",
                Errors = errors,
                TraceId = traceId,
            })
            {
                StatusCode = code,
                DeclaredType = typeof(ApiResponse),
            };
        }

        // 3. Object thường → wrap thành ApiResponse success / error theo status code
        var wrapped = statusCode >= 400
            ? new ApiResponse
            {
                Status = ApiStatus.Error,
                Code = statusCode,
                Message = value as string,
                Data = value is string ? null : value,
                TraceId = traceId,
            }
            : new ApiResponse
            {
                Status = ApiStatus.Success,
                Code = statusCode,
                Data = value,
            };

        return new ObjectResult(wrapped)
        {
            StatusCode = statusCode,
            DeclaredType = typeof(ApiResponse),
        };
    }

    private static ObjectResult WrapStatusCodeResult(int statusCode)
    {
        var wrapped = statusCode >= 400
            ? ApiResponse.Fail(message: ReasonPhrases.Get(statusCode), code: statusCode)
            : ApiResponse.Ok(code: statusCode);

        return new ObjectResult(wrapped)
        {
            StatusCode = statusCode,
            DeclaredType = typeof(ApiResponse),
        };
    }

    private static bool IsGenericApiResponse(Type type)
    {
        for (var t = type; t is not null; t = t.BaseType!)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ApiResponse<>))
            {
                return true;
            }
        }
        return false;
    }
}

/// <summary>HTTP reason phrase helper — tránh phụ thuộc Microsoft.AspNetCore.WebUtilities.</summary>
internal static class ReasonPhrases
{
    public static string Get(int code) => code switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status422UnprocessableEntity => "Unprocessable Entity",
        StatusCodes.Status429TooManyRequests => "Too Many Requests",
        StatusCodes.Status500InternalServerError => "Internal Server Error",
        StatusCodes.Status502BadGateway => "Bad Gateway",
        StatusCodes.Status503ServiceUnavailable => "Service Unavailable",
        _ => $"HTTP {code}",
    };
}
