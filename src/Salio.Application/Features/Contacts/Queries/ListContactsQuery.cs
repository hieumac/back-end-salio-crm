using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Application.Features.Contacts.Dtos;
using Salio.Domain.Common;

namespace Salio.Application.Features.Contacts.Queries;

public record ListContactsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? CompanyId = null,
    string? SortBy = "updatedAt",
    string? SortDir = "desc") : IRequest<PagedResult<ContactListItemDto>>;

public class ListContactsQueryValidator : AbstractValidator<ListContactsQuery>
{
    public ListContactsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class ListContactsQueryHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<ListContactsQuery, PagedResult<ContactListItemDto>>
{
    public async Task<PagedResult<ContactListItemDto>> Handle(ListContactsQuery q, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var query = db.Contacts.AsNoTracking()
            .Where(c => c.OrgId == current.OrgId && c.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(c =>
                EF.Functions.ILike(c.FullName, $"%{s}%") ||
                (c.Email != null && EF.Functions.ILike(c.Email, $"%{s}%")) ||
                (c.Phone != null && EF.Functions.ILike(c.Phone, $"%{s}%")));
        }
        if (q.CompanyId.HasValue)
            query = query.Where(c => c.CompanyId == q.CompanyId);

        query = (q.SortBy, q.SortDir) switch
        {
            ("name", "asc") => query.OrderBy(c => c.FullName),
            ("name", _) => query.OrderByDescending(c => c.FullName),
            (_, "asc") => query.OrderBy(c => c.UpdatedAt),
            _ => query.OrderByDescending(c => c.UpdatedAt),
        };

        var total = await query.LongCountAsync(ct);
        var items = await query
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(c => new ContactListItemDto(
                c.Id, c.FullName, c.Email, c.Phone, c.Title,
                c.CompanyId,
                c.Company == null ? null : c.Company.Name,
                c.IsPrimary))
            .ToListAsync(ct);

        return new PagedResult<ContactListItemDto> { Items = items, Page = q.Page, PageSize = q.PageSize, Total = total };
    }
}
