using DealTaskStatus = Salio.Domain.Enums.TaskStatus;
using DealTaskPriority = Salio.Domain.Enums.TaskPriority;

namespace Salio.Application.Features.Tasks.Dtos;

public record TaskDto(
    Guid Id,
    string Title,
    string? Description,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid? DealId,
    string? DealTitle,
    DateTimeOffset? DueAt,
    DateTimeOffset? CompletedAt,
    DealTaskPriority Priority,
    DealTaskStatus Status,
    DateTimeOffset CreatedAt);

public record TaskListItemDto(
    Guid Id,
    string Title,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid? DealId,
    DateTimeOffset? DueAt,
    DealTaskPriority Priority,
    DealTaskStatus Status);
