using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Application.Features.Users.Dtos;
using Salio.Domain.Common;

namespace Salio.Application.Features.Users.Queries;

public record GetMeQuery() : IRequest<UserMeDto>;

public class GetMeQueryHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<GetMeQuery, UserMeDto>
{
    public async Task<UserMeDto> Handle(GetMeQuery q, CancellationToken ct)
    {
        if (current.UserId is null) throw new ForbiddenException("Not authenticated");

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == current.UserId && u.DeletedAt == null, ct)
            ?? throw new NotFoundException("User", current.UserId.Value);

        string? orgName = null;
        if (current.OrgId.HasValue)
        {
            orgName = await db.Organizations.AsNoTracking()
                .Where(o => o.Id == current.OrgId)
                .Select(o => o.Name)
                .FirstOrDefaultAsync(ct);
        }

        var roles = current.OrgId.HasValue
            ? await db.UserRoles.AsNoTracking()
                .Where(ur => ur.UserId == user.Id && ur.OrgId == current.OrgId
                            && (ur.ExpiresAt == null || ur.ExpiresAt > DateTimeOffset.UtcNow))
                .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Code)
                .ToListAsync(ct)
            : new List<string>();

        return new UserMeDto(
            user.Id, user.Email, user.FullName, user.AvatarUrl, user.EmailVerified,
            current.OrgId, orgName, roles, user.LastLoginAt);
    }
}
