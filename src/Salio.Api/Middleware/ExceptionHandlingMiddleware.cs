using System.Net;
using System.Text.Json;
using FluentValidation;
using Salio.Api.Common;
using Salio.Domain.Common;

namespace Salio.Api.Middleware;

/// <summary>
/// Global exception → trả về JSON theo format <see cref="ApiResponse"/>:
/// <code>
/// {
///   "status": "error",
///   "code": 422,
///   "message": "Validation failed",
///   "errors": { "code": "VALIDATION", "details": [...] },
///   "traceId": "00-abc..."
/// }
/// </code>
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (FluentValidation.ValidationException vex)
        {
            await WriteAsync(ctx, HttpStatusCode.UnprocessableEntity, "VALIDATION", "Validation failed",
                vex.Errors.Select(e => new { field = e.PropertyName, error = e.ErrorMessage }));
        }
        catch (NotFoundException nex)
        {
            await WriteAsync(ctx, HttpStatusCode.NotFound, nex.Code ?? "NOT_FOUND", nex.Message);
        }
        catch (ForbiddenException fex)
        {
            await WriteAsync(ctx, HttpStatusCode.Forbidden, fex.Code ?? "FORBIDDEN", fex.Message);
        }
        catch (ConflictException cex)
        {
            await WriteAsync(ctx, HttpStatusCode.Conflict, cex.Code ?? "CONFLICT", cex.Message);
        }
        catch (DomainException dex)
        {
            await WriteAsync(ctx, HttpStatusCode.BadRequest, dex.Code ?? "DOMAIN_ERROR", dex.Message);
        }
        catch (UnauthorizedAccessException uex)
        {
            await WriteAsync(ctx, HttpStatusCode.Unauthorized, "UNAUTHORIZED", uex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteAsync(ctx, HttpStatusCode.InternalServerError, "INTERNAL", "Internal server error");
        }
    }

    private static Task WriteAsync(HttpContext ctx, HttpStatusCode status, string errorCode, string message, object? details = null)
    {
        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/json";

        var body = new ApiResponse
        {
            Status = ApiStatus.Error,
            Code = (int)status,
            Message = message,
            Errors = new { code = errorCode, details },
            TraceId = ctx.TraceIdentifier,
        };

        return ctx.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOpts));
    }
}
