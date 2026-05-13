using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Salio.Api.Common;
using Salio.Api.Common.Authorization;
using Salio.Application.Features.Organizations.Commands;
using Salio.Application.Features.Organizations.Dtos;
using Salio.Application.Features.Organizations.Queries;

namespace Salio.Api.Controllers.V1;

/// <summary>Organizations — /api/v1/organizations</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/organizations")]
[Authorize]
public class OrganizationsController : ApiControllerBase
{
    /// <summary>Lấy thông tin org hiện tại của user.</summary>
    [HttpGet("current")]
    [ProducesResponseType(typeof(ApiResponse<OrganizationDto>), 200)]
    public async Task<IActionResult> Current(CancellationToken ct)
    {
        var dto = await Mediator.Send(new GetCurrentOrganizationQuery(), ct);
        return Ok(ApiResponse<OrganizationDto>.Ok(dto));
    }

    /// <summary>Cập nhật thông tin org hiện tại.</summary>
    [HttpPut("current")]
    [RequirePermission("settings.organization", "update")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> UpdateCurrent([FromBody] UpdateOrganizationCommand cmd, CancellationToken ct)
    {
        await Mediator.Send(cmd, ct);
        return Ok(ApiResponse.Ok("Updated"));
    }
}
