using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Application.Features.Deals.Dtos;
using Salio.Domain.Common;

namespace Salio.Application.Features.Deals.Queries;

public record GetDealByIdQuery(Guid Id) : IRequest<DealDto>;

public class GetDealByIdQueryHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<GetDealByIdQuery, DealDto>
{
    public async Task<DealDto> Handle(GetDealByIdQuery req, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var d = await db.Deals.AsNoTracking()
            .Where(x => x.Id == req.Id && x.OrgId == current.OrgId && x.DeletedAt == null)
            .Select(x => new DealDto(
                x.Id, x.Code, x.Title, x.Value, x.Currency, x.Probability,
                x.PipelineId, x.StageId, x.Stage!.Name,
                x.CompanyId, x.Company == null ? null : x.Company.Name,
                x.ContactId, x.Contact == null ? null : x.Contact.FullName,
                x.AssigneeId, x.Assignee == null ? null : x.Assignee.FullName,
                x.ExpectedCloseDate, x.AiScore, x.LastActivityAt, x.CreatedAt))
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Deal", req.Id);

        return d;
    }
}
