using System.Net;
using System.Text.Json;
using FluentValidation;
using Salio.Domain.Common;

namespace Salio.Api.Middleware;

/// <summary>
/// Global exception → JSON ProblemDetails-like response chuẩn.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (ValidationException vex)
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteAsync(ctx, HttpStatusCode.InternalServerError, "INTERNAL", "Internal server error");
        }
    }

    private static Task WriteAsync(HttpContext ctx, HttpStatusCode status, string code, string message, object? details = null)
    {
        ctx.Response.StatusCode = (int)status;
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            success = false,
            error = new { code, message, details },
            traceId = ctx.TraceIdentifier,
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
