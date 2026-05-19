using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Salio.Api.Swagger;

/// <summary>
/// Chỉ áp dụng yêu cầu Bearer token cho các endpoint có <c>[Authorize]</c>
/// và bỏ qua các endpoint có <c>[AllowAnonymous]</c>.
/// Đồng thời tự bổ sung response 401/403 cho các endpoint cần auth.
/// </summary>
public sealed class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var endpointMetadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

        var hasAllowAnonymous = endpointMetadata.OfType<AllowAnonymousAttribute>().Any();
        var hasAuthorize = endpointMetadata.OfType<AuthorizeAttribute>().Any();

        if (hasAllowAnonymous || !hasAuthorize)
        {
            return;
        }

        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Unauthorized — token thiếu hoặc không hợp lệ" });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Forbidden — không đủ quyền" });

        var bearerScheme = new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
            {
                Type = ReferenceType.SecurityScheme,
                Id = "Bearer",
            },
        };

        operation.Security =
        [
            new OpenApiSecurityRequirement
            {
                [bearerScheme] = Array.Empty<string>(),
            },
        ];
    }
}
