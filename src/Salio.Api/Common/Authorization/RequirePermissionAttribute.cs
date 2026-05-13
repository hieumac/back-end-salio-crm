using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Salio.Application.Common.Interfaces;

namespace Salio.Api.Common.Authorization;

/// <summary>
/// [RequirePermission("crm.deals.list", "create")] — chặn nếu user không có quyền.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequirePermissionAttribute(string functionCode, string actionCode)
    : Attribute, IAsyncAuthorizationFilter
{
    public string FunctionCode { get; } = functionCode;
    public string ActionCode { get; } = actionCode;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext ctx)
    {
        var current = ctx.HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();
        if (!current.IsAuthenticated || current.UserId is null || current.OrgId is null)
        {
            ctx.Result = new UnauthorizedResult();
            return;
        }

        var checker = ctx.HttpContext.RequestServices.GetRequiredService<IPermissionChecker>();
        var allowed = await checker.HasPermissionAsync(current.UserId.Value, current.OrgId.Value, FunctionCode, ActionCode);
        if (!allowed)
        {
            ctx.Result = new ObjectResult(new
            {
                success = false,
                error = new { code = "FORBIDDEN", message = $"Missing permission {FunctionCode}:{ActionCode}" },
            })
            { StatusCode = 403 };
        }
    }
}
