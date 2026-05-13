using FluentValidation;
using MediatR;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;
using TaskEntity = Salio.Domain.Entities.Models.Task;
using DealTaskStatus = Salio.Domain.Enums.TaskStatus;
using DealTaskPriority = Salio.Domain.Enums.TaskPriority;

namespace Salio.Application.Features.Tasks.Commands;

public record CreateTaskCommand(
    string Title,
    string? Description,
    Guid? AssigneeId,
    Guid? DealId,
    DateTimeOffset? DueAt,
    DealTaskPriority Priority = DealTaskPriority.Medium) : IRequest<Guid>;

public class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(255);
    }
}

public class CreateTaskCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<CreateTaskCommand, Guid>
{
    public async Task<Guid> Handle(CreateTaskCommand cmd, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var task = new TaskEntity
        {
            OrgId = current.OrgId.Value,
            Title = cmd.Title.Trim(),
            Description = cmd.Description?.Trim(),
            AssigneeId = cmd.AssigneeId ?? current.UserId,
            DealId = cmd.DealId,
            DueAt = cmd.DueAt,
            Priority = cmd.Priority,
            Status = DealTaskStatus.Pending,
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
        return task.Id;
    }
}
