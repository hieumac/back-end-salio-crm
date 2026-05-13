using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;

namespace Salio.Application.Features.Tasks.Commands;

public record DeleteTaskCommand(Guid Id) : IRequest<Unit>;

public class DeleteTaskCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<DeleteTaskCommand, Unit>
{
    public async Task<Unit> Handle(DeleteTaskCommand cmd, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var task = await db.Tasks.FirstOrDefaultAsync(
            t => t.Id == cmd.Id && t.OrgId == current.OrgId && t.DeletedAt == null, ct)
            ?? throw new NotFoundException("Task", cmd.Id);

        db.Tasks.Remove(task);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
