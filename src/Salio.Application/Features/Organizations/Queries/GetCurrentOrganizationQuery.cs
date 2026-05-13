using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Application.Features.Organizations.Dtos;
using Salio.Domain.Common;

namespace Salio.Application.Features.Organizations.Queries;

public record GetCurrentOrganizationQuery() : IRequest<OrganizationDto>;

public class GetCurrentOrganizationQueryHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<GetCurrentOrganizationQuery, OrganizationDto>
{
    public async Task<OrganizationDto> Handle(GetCurrentOrganizationQuery q, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var org = await db.Organizations.AsNoTracking()
            .Where(o => o.Id == current.OrgId)
            .Select(o => new OrganizationDto(
                o.Id, o.Slug, o.Name, o.Plan, o.Locale,
                o.Members.Count(m => m.IsActive),
                o.CreatedAt))
            .FirstOrDefaultAsync(ct);

        return org ?? throw new NotFoundException("Organization", current.OrgId.Value);
    }
}
