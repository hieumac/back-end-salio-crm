using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Application.Features.Deals.Dtos;
using Salio.Domain.Common;

namespace Salio.Application.Features.Deals.Queries;

public record ListDealsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? PipelineId = null,
    Guid? StageId = null,
    Guid? AssigneeId = null,
    string? SortBy = "createdAt",
    string? SortDir = "desc") : IRequest<PagedResult<DealListItemDto>>;

public class ListDealsQueryHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<ListDealsQuery, PagedResult<DealListItemDto>>
{
    public async Task<PagedResult<DealListItemDto>> Handle(ListDealsQuery q, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var query = db.Deals.AsNoTracking()
            .Where(d => d.OrgId == current.OrgId && d.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(d => EF.Functions.ILike(d.Title, $"%{s}%") || EF.Functions.ILike(d.Code, $"%{s}%"));
        }
        if (q.PipelineId.HasValue) query = query.Where(d => d.PipelineId == q.PipelineId);
        if (q.StageId.HasValue) query = query.Where(d => d.StageId == q.StageId);
        if (q.AssigneeId.HasValue) query = query.Where(d => d.AssigneeId == q.AssigneeId);

        query = (q.SortBy, q.SortDir) switch
        {
            ("value", "asc") => query.OrderBy(d => d.Value),
            ("value", _) => query.OrderByDescending(d => d.Value),
            ("aiScore", "asc") => query.OrderBy(d => d.AiScore),
            ("aiScore", _) => query.OrderByDescending(d => d.AiScore),
            (_, "asc") => query.OrderBy(d => d.CreatedAt),
            _ => query.OrderByDescending(d => d.CreatedAt),
        };

        var total = await query.LongCountAsync(ct);
        var items = await query
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(d => new DealListItemDto(
                d.Id, d.Code, d.Title, d.Value, d.Currency,
                d.StageId, d.Stage!.Name,
                d.Company == null ? null : d.Company.Name,
                d.AiScore, d.ExpectedCloseDate))
            .ToListAsync(ct);

        return new PagedResult<DealListItemDto>
        {
            Items = items,
            Page = q.Page,
            PageSize = q.PageSize,
            Total = total,
        };
    }
}
