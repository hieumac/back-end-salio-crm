using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Salio.Api.Common;
using Salio.Api.Common.Authorization;
using Salio.Application.Features.Users.Dtos;
using Salio.Application.Features.Users.Queries;
using Salio.Domain.Common;

namespace Salio.Api.Controllers.V1;

/// <summary>Users — /api/v1/users</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
[Authorize]
public class UsersController : ApiControllerBase
{
    /// <summary>Lấy thông tin user đang đăng nhập + org + roles.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserMeDto>), 200)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var dto = await Mediator.Send(new GetMeQuery(), ct);
        return Ok(ApiResponse<UserMeDto>.Ok(dto));
    }

    /// <summary>Liệt kê user thuộc org hiện tại.</summary>
    [HttpGet]
    [RequirePermission("settings.users", "view")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<UserListItemDto>>), 200)]
    public async Task<IActionResult> List([FromQuery] ListUsersQuery q, CancellationToken ct)
    {
        var result = await Mediator.Send(q, ct);
        return Ok(ApiResponse<PagedResult<UserListItemDto>>.Ok(result));
    }
}
