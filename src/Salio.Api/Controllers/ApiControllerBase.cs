using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Salio.Api.Controllers;

/// <summary>
/// Base controller — chuẩn route `/api/v{version}/{controller}` với versioning.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}
