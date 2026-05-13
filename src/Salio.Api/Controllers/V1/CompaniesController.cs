using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Salio.Api.Common;
using Salio.Api.Common.Authorization;
using Salio.Application.Features.Companies.Commands;
using Salio.Application.Features.Companies.Dtos;
using Salio.Application.Features.Companies.Queries;
using Salio.Domain.Common;

namespace Salio.Api.Controllers.V1;

/// <summary>CRM Companies — /api/v1/crm/companies</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/crm/companies")]
[Authorize]
public class CompaniesController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission("crm.companies", "view")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CompanyListItemDto>>), 200)]
    public async Task<IActionResult> List([FromQuery] ListCompaniesQuery q, CancellationToken ct)
    {
        var result = await Mediator.Send(q, ct);
        return Ok(ApiResponse<PagedResult<CompanyListItemDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("crm.companies", "view")]
    [ProducesResponseType(typeof(ApiResponse<CompanyDto>), 200)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await Mediator.Send(new GetCompanyByIdQuery(id), ct);
        return Ok(ApiResponse<CompanyDto>.Ok(dto));
    }

    [HttpPost]
    [RequirePermission("crm.companies", "create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), 201)]
    public async Task<IActionResult> Create([FromBody] CreateCompanyCommand cmd, CancellationToken ct)
    {
        var id = await Mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id, version = "1.0" }, ApiResponse<Guid>.Ok(id));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("crm.companies", "update")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCompanyCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest(ApiResponse.Fail("Id route khác Id body"));
        await Mediator.Send(cmd, ct);
        return Ok(ApiResponse.Ok("Updated"));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("crm.companies", "delete")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteCompanyCommand(id), ct);
        return Ok(ApiResponse.Ok("Deleted"));
    }
}
