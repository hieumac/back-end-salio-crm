using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;

namespace Salio.Application.Features.Contacts.Commands;

public record DeleteContactCommand(Guid Id) : IRequest<Unit>;

public class DeleteContactCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<DeleteContactCommand, Unit>
{
    public async Task<Unit> Handle(DeleteContactCommand cmd, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var contact = await db.Contacts.FirstOrDefaultAsync(
            c => c.Id == cmd.Id && c.OrgId == current.OrgId && c.DeletedAt == null, ct)
            ?? throw new NotFoundException("Contact", cmd.Id);

        db.Contacts.Remove(contact);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
