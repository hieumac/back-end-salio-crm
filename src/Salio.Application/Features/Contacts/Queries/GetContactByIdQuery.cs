using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Application.Features.Contacts.Dtos;
using Salio.Domain.Common;

namespace Salio.Application.Features.Contacts.Queries;

public record GetContactByIdQuery(Guid Id) : IRequest<ContactDto>;

public class GetContactByIdQueryHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<GetContactByIdQuery, ContactDto>
{
    public async Task<ContactDto> Handle(GetContactByIdQuery q, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var dto = await db.Contacts.AsNoTracking()
            .Where(c => c.Id == q.Id && c.OrgId == current.OrgId && c.DeletedAt == null)
            .Select(c => new ContactDto(
                c.Id, c.CompanyId,
                c.Company == null ? null : c.Company.Name,
                c.FullName, c.Email, c.Phone, c.Title, c.IsPrimary,
                c.CreatedAt, c.UpdatedAt))
            .FirstOrDefaultAsync(ct);

        return dto ?? throw new NotFoundException("Contact", q.Id);
    }
}
