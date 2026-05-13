using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Salio.Api.Common;
using Salio.Api.Common.Authorization;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Enums;

namespace Salio.Api.Controllers.V1;

/// <summary>
/// Quản lý system functions & actions (cho RBAC matrix UI) — /api/v1/system/functions
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system/functions")]
[Authorize]
public class SystemFunctionsController(ISalioDbContext db) : ApiControllerBase
{
    public record FunctionDto(Guid Id, string Code, string Name, SystemModuleGroup ModuleGroup,
        FunctionRiskLevel RiskLevel, string? Path, IReadOnlyList<ActionDto> Actions);
    public record ActionDto(Guid Id, string Code, string Name);

    /// <summary>List functions kèm theo các action được phép trên mỗi function (matrix data cho UI).</summary>
    [HttpGet]
    [RequirePermission("system.functions", "view")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<FunctionDto>>), 200)]
    public async Task<IActionResult> ListFunctions(CancellationToken ct)
    {
        var data = await db.SystemFunctions
            .Where(f => f.IsActive)
            .OrderBy(f => f.ModuleGroup).ThenBy(f => f.Order)
            .Select(f => new FunctionDto(
                f.Id, f.Code, f.Name, f.ModuleGroup, f.RiskLevel, f.Path,
                f.FunctionActions.Select(fa => new ActionDto(fa.Action!.Id, fa.Action.Code, fa.Action.Name)).ToList()))
            .ToListAsync(ct);

        return Ok(ApiResponse<IEnumerable<FunctionDto>>.Ok(data));
    }

    /// <summary>List actions chuẩn của hệ thống.</summary>
    [HttpGet("/api/v{version:apiVersion}/system/actions")]
    [RequirePermission("system.functions", "view")]
    public async Task<IActionResult> ListActions(CancellationToken ct)
    {
        var data = await db.SystemActions
            .OrderBy(a => a.Order)
            .Select(a => new ActionDto(a.Id, a.Code, a.Name))
            .ToListAsync(ct);
        return Ok(ApiResponse<IEnumerable<ActionDto>>.Ok(data));
    }
}
