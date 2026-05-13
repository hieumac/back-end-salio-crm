using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;

namespace Salio.Application.Features.Companies.Commands;

public record DeleteCompanyCommand(Guid Id) : IRequest<Unit>;

public class DeleteCompanyCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<DeleteCompanyCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCompanyCommand cmd, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var company = await db.Companies.FirstOrDefaultAsync(
            c => c.Id == cmd.Id && c.OrgId == current.OrgId && c.DeletedAt == null, ct)
            ?? throw new NotFoundException("Company", cmd.Id);

        // Check ràng buộc: không cho xóa nếu còn deal active
        var hasDeals = await db.Deals.AnyAsync(d => d.CompanyId == cmd.Id && d.DeletedAt == null, ct);
        if (hasDeals)
            throw new ConflictException("Company has active deals — reassign or close them first");

        db.Companies.Remove(company);   // soft delete qua SaveChanges override
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
