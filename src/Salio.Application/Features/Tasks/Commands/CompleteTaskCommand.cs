using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;
using DealTaskStatus = Salio.Domain.Enums.TaskStatus;

namespace Salio.Application.Features.Tasks.Commands;

public record CompleteTaskCommand(Guid Id) : IRequest<Unit>;

public class CompleteTaskCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<CompleteTaskCommand, Unit>
{
    public async Task<Unit> Handle(CompleteTaskCommand cmd, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var task = await db.Tasks.FirstOrDefaultAsync(
            t => t.Id == cmd.Id && t.OrgId == current.OrgId && t.DeletedAt == null, ct)
            ?? throw new NotFoundException("Task", cmd.Id);

        if (task.Status == DealTaskStatus.Done) return Unit.Value;   // idempotent

        task.Status = DealTaskStatus.Done;
        task.CompletedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
