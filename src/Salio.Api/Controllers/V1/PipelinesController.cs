using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Salio.Api.Common;
using Salio.Api.Common.Authorization;
using Salio.Application.Features.Pipelines.Commands;
using Salio.Application.Features.Pipelines.Dtos;
using Salio.Application.Features.Pipelines.Queries;

namespace Salio.Api.Controllers.V1;

/// <summary>CRM Pipelines + Stages — /api/v1/crm/pipelines</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/crm/pipelines")]
[Authorize]
public class PipelinesController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission("crm.pipelines", "view")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PipelineDto>>), 200)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await Mediator.Send(new ListPipelinesQuery(), ct);
        return Ok(ApiResponse<IReadOnlyList<PipelineDto>>.Ok(result));
    }

    [HttpPost]
    [RequirePermission("crm.pipelines", "create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), 201)]
    public async Task<IActionResult> Create([FromBody] CreatePipelineCommand cmd, CancellationToken ct)
    {
        var id = await Mediator.Send(cmd, ct);
        return Created($"/api/v1/crm/pipelines/{id}", ApiResponse<Guid>.Ok(id));
    }
}
