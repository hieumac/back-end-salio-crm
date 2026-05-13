using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Salio.Api.Common;
using Salio.Application.Features.Auth.Commands;

namespace Salio.Api.Controllers.V1;

/// <summary>
/// Authentication endpoints — /api/v1/auth
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[AllowAnonymous]
public class AuthController : ApiControllerBase
{
    /// <summary>Đăng nhập bằng email + password.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), 200)]
    public async Task<IActionResult> Login([FromBody] LoginCommand cmd, CancellationToken ct)
    {
        var result = await Mediator.Send(cmd, ct);
        return Ok(ApiResponse<LoginResponse>.Ok(result));
    }

    /// <summary>Đăng ký user + tạo organization mới.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), 201)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand cmd, CancellationToken ct)
    {
        var userId = await Mediator.Send(cmd, ct);
        return StatusCode(201, ApiResponse<Guid>.Ok(userId, "User registered"));
    }

    /// <summary>Refresh access token bằng refresh token.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), 200)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand cmd, CancellationToken ct)
    {
        var result = await Mediator.Send(cmd, ct);
        return Ok(ApiResponse<LoginResponse>.Ok(result));
    }
}
