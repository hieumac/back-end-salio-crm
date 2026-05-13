using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Application.Features.Companies.Dtos;
using Salio.Domain.Common;

namespace Salio.Application.Features.Companies.Queries;

public record GetCompanyByIdQuery(Guid Id) : IRequest<CompanyDto>;

public class GetCompanyByIdQueryHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<GetCompanyByIdQuery, CompanyDto>
{
    public async Task<CompanyDto> Handle(GetCompanyByIdQuery q, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var c = await db.Companies.AsNoTracking()
            .Where(x => x.Id == q.Id && x.OrgId == current.OrgId && x.DeletedAt == null)
            .Select(x => new CompanyDto(
                x.Id, x.Name, x.TaxCode, x.Industry, x.Size,
                x.Website, x.Phone, x.Email, x.Address,
                x.OwnerId, x.Owner == null ? null : x.Owner.FullName,
                x.Deals.Count(d => d.DeletedAt == null),
                x.Contacts.Count(co => co.DeletedAt == null),
                x.CreatedAt, x.UpdatedAt))
            .FirstOrDefaultAsync(ct);

        return c ?? throw new NotFoundException("Company", q.Id);
    }
}
