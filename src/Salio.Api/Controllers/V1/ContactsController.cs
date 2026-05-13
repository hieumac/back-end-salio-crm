using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Salio.Api.Common;
using Salio.Api.Common.Authorization;
using Salio.Application.Features.Contacts.Commands;
using Salio.Application.Features.Contacts.Dtos;
using Salio.Application.Features.Contacts.Queries;
using Salio.Domain.Common;

namespace Salio.Api.Controllers.V1;

/// <summary>CRM Contacts — /api/v1/crm/contacts</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/crm/contacts")]
[Authorize]
public class ContactsController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission("crm.contacts", "view")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ContactListItemDto>>), 200)]
    public async Task<IActionResult> List([FromQuery] ListContactsQuery q, CancellationToken ct)
    {
        var result = await Mediator.Send(q, ct);
        return Ok(ApiResponse<PagedResult<ContactListItemDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("crm.contacts", "view")]
    [ProducesResponseType(typeof(ApiResponse<ContactDto>), 200)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await Mediator.Send(new GetContactByIdQuery(id), ct);
        return Ok(ApiResponse<ContactDto>.Ok(dto));
    }

    [HttpPost]
    [RequirePermission("crm.contacts", "create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), 201)]
    public async Task<IActionResult> Create([FromBody] CreateContactCommand cmd, CancellationToken ct)
    {
        var id = await Mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetById), new { id, version = "1.0" }, ApiResponse<Guid>.Ok(id));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("crm.contacts", "update")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContactCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest(ApiResponse.Fail("Id route khác Id body"));
        await Mediator.Send(cmd, ct);
        return Ok(ApiResponse.Ok("Updated"));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("crm.contacts", "delete")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await Mediator.Send(new DeleteContactCommand(id), ct);
        return Ok(ApiResponse.Ok("Deleted"));
    }
}
