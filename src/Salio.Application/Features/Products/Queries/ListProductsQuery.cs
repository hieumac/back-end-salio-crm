using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Application.Features.Products.Dtos;
using Salio.Domain.Common;

namespace Salio.Application.Features.Products.Queries;

public record ListProductsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool? IsActive = null) : IRequest<PagedResult<ProductListItemDto>>;

public class ListProductsQueryValidator : AbstractValidator<ListProductsQuery>
{
    public ListProductsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class ListProductsQueryHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<ListProductsQuery, PagedResult<ProductListItemDto>>
{
    public async Task<PagedResult<ProductListItemDto>> Handle(ListProductsQuery q, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var query = db.Products.AsNoTracking()
            .Where(p => p.OrgId == current.OrgId && p.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, $"%{s}%") || EF.Functions.ILike(p.Code, $"%{s}%"));
        }
        if (q.IsActive.HasValue)
            query = query.Where(p => p.IsActive == q.IsActive);

        var total = await query.LongCountAsync(ct);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(p => new ProductListItemDto(
                p.Id, p.Code, p.Name, p.UnitPrice, p.Unit, p.Currency, p.IsActive))
            .ToListAsync(ct);

        return new PagedResult<ProductListItemDto> { Items = items, Page = q.Page, PageSize = q.PageSize, Total = total };
    }
}
