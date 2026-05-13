using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Application.Features.Tasks.Dtos;
using Salio.Domain.Common;
using DealTaskStatus = Salio.Domain.Enums.TaskStatus;

namespace Salio.Application.Features.Tasks.Queries;

public record ListTasksQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    Guid? AssigneeId = null,
    Guid? DealId = null,
    DealTaskStatus? Status = null,
    bool? MineOnly = null) : IRequest<PagedResult<TaskListItemDto>>;

public class ListTasksQueryValidator : AbstractValidator<ListTasksQuery>
{
    public ListTasksQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class ListTasksQueryHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<ListTasksQuery, PagedResult<TaskListItemDto>>
{
    public async Task<PagedResult<TaskListItemDto>> Handle(ListTasksQuery q, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var query = db.Tasks.AsNoTracking()
            .Where(t => t.OrgId == current.OrgId && t.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(t => EF.Functions.ILike(t.Title, $"%{s}%"));
        }
        if (q.AssigneeId.HasValue) query = query.Where(t => t.AssigneeId == q.AssigneeId);
        if (q.DealId.HasValue) query = query.Where(t => t.DealId == q.DealId);
        if (q.Status.HasValue) query = query.Where(t => t.Status == q.Status);
        if (q.MineOnly == true && current.UserId.HasValue)
            query = query.Where(t => t.AssigneeId == current.UserId);

        var total = await query.LongCountAsync(ct);
        var items = await query
            .OrderBy(t => t.Status == DealTaskStatus.Done)   // done xuống dưới
            .ThenBy(t => t.DueAt)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(t => new TaskListItemDto(
                t.Id, t.Title, t.AssigneeId,
                t.Assignee == null ? null : t.Assignee.FullName,
                t.DealId, t.DueAt, t.Priority, t.Status))
            .ToListAsync(ct);

        return new PagedResult<TaskListItemDto> { Items = items, Page = q.Page, PageSize = q.PageSize, Total = total };
    }
}
