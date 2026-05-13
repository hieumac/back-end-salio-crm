using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Salio.Api.Common;
using Salio.Api.Common.Authorization;
using Salio.Application.Features.Tasks.Commands;
using Salio.Application.Features.Tasks.Dtos;
using Salio.Application.Features.Tasks.Queries;
using Salio.Domain.Common;

namespace Salio.Api.Controllers.V1;

/// <summary>Tasks — /api/v1/tasks</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tasks")]
[Authorize]
public class TasksController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission("crm.tasks", "view")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TaskListItemDto>>), 200)]
    public async Task<IActionResult> List([FromQuery] ListTasksQuery q, CancellationToken ct)
    {
        var result = await Mediator.Send(q, ct);
        return Ok(ApiResponse<PagedResult<TaskListItemDto>>.Ok(result));
    }

    [HttpPost]
    [RequirePermission("crm.tasks", "create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), 201)]
    public async Task<IActionResult> Create([FromBody] CreateTaskCommand cmd, CancellationToken ct)
    {
        var id = await Mediator.Send(cmd, ct);
        return Created($"/api/v1/tasks/{id}", ApiResponse<Guid>.Ok(id));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("crm.tasks", "update")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTaskCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest(ApiResponse.Fail("Id route khác Id body"));
        await Mediator.Send(cmd, ct);
        return Ok(ApiResponse.Ok("Updated"));
    }

    [HttpPost("{id:guid}/complete")]
    [RequirePermission("crm.tasks", "update")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new CompleteTaskCommand(id), ct);
        return Ok(ApiResponse.Ok("Completed"));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("crm.tasks", "delete")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteTaskCommand(id), ct);
        return Ok(ApiResponse.Ok("Deleted"));
    }
}
