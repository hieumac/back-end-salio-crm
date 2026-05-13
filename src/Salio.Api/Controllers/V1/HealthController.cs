using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Salio.Api.Common;

namespace Salio.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/health")]
[AllowAnonymous]
public class HealthController : ApiControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(ApiResponse<object>.Ok(new
    {
        status = "ok",
        version = "1.0",
        time = DateTimeOffset.UtcNow,
    }));
}
