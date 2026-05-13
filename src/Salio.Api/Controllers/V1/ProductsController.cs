using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Salio.Api.Common;
using Salio.Api.Common.Authorization;
using Salio.Application.Features.Products.Commands;
using Salio.Application.Features.Products.Dtos;
using Salio.Application.Features.Products.Queries;
using Salio.Domain.Common;

namespace Salio.Api.Controllers.V1;

/// <summary>CRM Products — /api/v1/crm/products</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/crm/products")]
[Authorize]
public class ProductsController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission("crm.products", "view")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ProductListItemDto>>), 200)]
    public async Task<IActionResult> List([FromQuery] ListProductsQuery q, CancellationToken ct)
    {
        var result = await Mediator.Send(q, ct);
        return Ok(ApiResponse<PagedResult<ProductListItemDto>>.Ok(result));
    }

    [HttpPost]
    [RequirePermission("crm.products", "create")]
    [ProducesResponseType(typeof(ApiResponse<Guid>), 201)]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand cmd, CancellationToken ct)
    {
        var id = await Mediator.Send(cmd, ct);
        return Created($"/api/v1/crm/products/{id}", ApiResponse<Guid>.Ok(id));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("crm.products", "update")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductCommand cmd, CancellationToken ct)
    {
        if (id != cmd.Id) return BadRequest(ApiResponse.Fail("Id route khác Id body"));
        await Mediator.Send(cmd, ct);
        return Ok(ApiResponse.Ok("Updated"));
    }
}
