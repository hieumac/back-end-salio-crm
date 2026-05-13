using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Application.Features.Users.Dtos;
using Salio.Domain.Common;

namespace Salio.Application.Features.Users.Queries;

public record ListUsersQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    bool? IsActive = null) : IRequest<PagedResult<UserListItemDto>>;

public class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class ListUsersQueryHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<ListUsersQuery, PagedResult<UserListItemDto>>
{
    public async Task<PagedResult<UserListItemDto>> Handle(ListUsersQuery q, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        // Lấy user qua OrgMember (chỉ user thuộc org hiện tại)
        var memberQuery = db.OrgMembers.AsNoTracking()
            .Where(m => m.OrgId == current.OrgId);

        var userQuery = memberQuery
            .Join(db.Users.AsNoTracking().Where(u => u.DeletedAt == null),
                m => m.UserId, u => u.Id, (m, u) => u);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            userQuery = userQuery.Where(u =>
                EF.Functions.ILike(u.Email, $"%{s}%") || EF.Functions.ILike(u.FullName, $"%{s}%"));
        }
        if (q.IsActive.HasValue)
            userQuery = userQuery.Where(u => u.IsActive == q.IsActive);

        var total = await userQuery.LongCountAsync(ct);
        var users = await userQuery
            .OrderBy(u => u.FullName)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(u => new { u.Id, u.Email, u.FullName, u.AvatarUrl, u.IsActive, u.LastLoginAt })
            .ToListAsync(ct);

        // Load role cho từng user (1 query, group bằng client)
        var userIds = users.Select(u => u.Id).ToList();
        var now = DateTimeOffset.UtcNow;
        var roleMap = await db.UserRoles.AsNoTracking()
            .Where(ur => userIds.Contains(ur.UserId) && ur.OrgId == current.OrgId
                        && (ur.ExpiresAt == null || ur.ExpiresAt > now))
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Code })
            .ToListAsync(ct);

        var items = users.Select(u => new UserListItemDto(
            u.Id, u.Email, u.FullName, u.AvatarUrl, u.IsActive,
            roleMap.Where(rm => rm.UserId == u.Id).Select(rm => rm.Code).ToList(),
            u.LastLoginAt)).ToList();

        return new PagedResult<UserListItemDto> { Items = items, Page = q.Page, PageSize = q.PageSize, Total = total };
    }
}
