using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Application.Features.Companies.Dtos;
using Salio.Domain.Common;

namespace Salio.Application.Features.Companies.Queries;

public record ListCompaniesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Industry = null,
    Guid? OwnerId = null,
    string? SortBy = "updatedAt",
    string? SortDir = "desc") : IRequest<PagedResult<CompanyListItemDto>>;

public class ListCompaniesQueryValidator : AbstractValidator<ListCompaniesQuery>
{
    public ListCompaniesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class ListCompaniesQueryHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<ListCompaniesQuery, PagedResult<CompanyListItemDto>>
{
    public async Task<PagedResult<CompanyListItemDto>> Handle(ListCompaniesQuery q, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var query = db.Companies.AsNoTracking()
            .Where(c => c.OrgId == current.OrgId && c.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, $"%{s}%") ||
                (c.Email != null && EF.Functions.ILike(c.Email, $"%{s}%")) ||
                (c.Phone != null && EF.Functions.ILike(c.Phone, $"%{s}%")));
        }
        if (!string.IsNullOrWhiteSpace(q.Industry))
            query = query.Where(c => c.Industry == q.Industry);
        if (q.OwnerId.HasValue)
            query = query.Where(c => c.OwnerId == q.OwnerId);

        query = (q.SortBy, q.SortDir) switch
        {
            ("name", "asc") => query.OrderBy(c => c.Name),
            ("name", _) => query.OrderByDescending(c => c.Name),
            ("createdAt", "asc") => query.OrderBy(c => c.CreatedAt),
            ("createdAt", _) => query.OrderByDescending(c => c.CreatedAt),
            (_, "asc") => query.OrderBy(c => c.UpdatedAt),
            _ => query.OrderByDescending(c => c.UpdatedAt),
        };

        var total = await query.LongCountAsync(ct);
        var items = await query
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(c => new CompanyListItemDto(
                c.Id, c.Name, c.Industry, c.Email, c.Phone,
                c.Owner == null ? null : c.Owner.FullName,
                c.Deals.Count(d => d.DeletedAt == null),
                c.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResult<CompanyListItemDto> { Items = items, Page = q.Page, PageSize = q.PageSize, Total = total };
    }
}
