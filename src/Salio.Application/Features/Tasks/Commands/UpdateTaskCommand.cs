using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;
using DealTaskStatus = Salio.Domain.Enums.TaskStatus;
using DealTaskPriority = Salio.Domain.Enums.TaskPriority;

namespace Salio.Application.Features.Tasks.Commands;

public record UpdateTaskCommand(
    Guid Id,
    string Title,
    string? Description,
    Guid? AssigneeId,
    Guid? DealId,
    DateTimeOffset? DueAt,
    DealTaskPriority Priority,
    DealTaskStatus Status) : IRequest<Unit>;

public class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
    }
}

public class UpdateTaskCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<UpdateTaskCommand, Unit>
{
    public async Task<Unit> Handle(UpdateTaskCommand cmd, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var task = await db.Tasks.FirstOrDefaultAsync(
            t => t.Id == cmd.Id && t.OrgId == current.OrgId && t.DeletedAt == null, ct)
            ?? throw new NotFoundException("Task", cmd.Id);

        task.Title = cmd.Title.Trim();
        task.Description = cmd.Description?.Trim();
        task.AssigneeId = cmd.AssigneeId;
        task.DealId = cmd.DealId;
        task.DueAt = cmd.DueAt;
        task.Priority = cmd.Priority;

        // Transition status
        if (task.Status != cmd.Status)
        {
            task.Status = cmd.Status;
            task.CompletedAt = cmd.Status == DealTaskStatus.Done ? DateTimeOffset.UtcNow : null;
        }

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
