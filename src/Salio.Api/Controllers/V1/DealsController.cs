using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Salio.Api.Common;
using Salio.Api.Common.Authorization;
using Salio.Application.Features.Deals.Commands;
using Salio.Application.Features.Deals.Dtos;
using Salio.Application.Features.Deals.Queries;
using Salio.Domain.Common;

namespace Salio.Api.Controllers.V1;

/// <summary>
/// CRM Deals endpoints — /api/v1/crm/deals
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/crm/deals")]
[Authorize]
public class DealsController : ApiControllerBase
{
    /// <summary>List deals (search/filter/pagination).</summary>
    [HttpGet]
    [RequirePermission("crm.deals.list", "view")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DealListItemDto>>), 200)]
    public async Task<IActionResult> List([FromQuery] ListDealsQuery q, CancellationToken ct)
    {
        var result = await Mediator.Send(q, ct);
        return Ok(ApiResponse<PagedResult<DealListItemDto>>.Ok(result));
    }

    /// <summary>Lấy chi tiết deal.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission("crm.deals.detail", "view")]
    [ProducesResponseType(typeof(ApiResponse<DealDto>), 200)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await Mediator.Send(new GetDealByIdQuery(id), ct);
        return Ok(ApiResponse<DealDto>.Ok(dto));
    }

    /// <summary>Tạo deal mới.</summary>
    [HttpPost]
    [RequirePermission("crm.deals.list", "create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), 201)]
    public async Task<IActionResult> Create([FromBody] CreateDealCommand cmd, CancellationToken ct)
    {
        var id = await Mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id, version = "1.0" }, ApiResponse<Guid>.Ok(id));
    }

    /// <summary>Di chuyển deal sang stage khác (Kanban drag-drop).</summary>
    [HttpPatch("{id:guid}/stage")]
    [RequirePermission("crm.deals.detail", "update")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> MoveStage(Guid id, [FromBody] MoveStageRequest body, CancellationToken ct)
    {
        await Mediator.Send(new MoveDealStageCommand(id, body.ToStageId, body.Note), ct);
        return Ok(ApiResponse.Ok("Stage moved"));
    }

    public record MoveStageRequest(Guid ToStageId, string? Note);
}
