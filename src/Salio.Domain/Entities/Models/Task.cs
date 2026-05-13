using Salio.Domain.Common;
using Salio.Domain.Entities.Crm;
using Salio.Domain.Entities.Identity;
using DealTaskStatus = Salio.Domain.Enums.TaskStatus;
using DealTaskPriority = Salio.Domain.Enums.TaskPriority;

namespace Salio.Domain.Entities.Models;

/// <summary>
/// Công việc — có thể độc lập hoặc gắn với deal.
/// Đặt trong namespace Models để tránh trùng với System.Threading.Tasks.Task.
/// </summary>
public class Task : TenantEntity
{
    public Organization? Organization { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    public Guid? DealId { get; set; }
    public Deal? Deal { get; set; }

    public DateTimeOffset? DueAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public DealTaskPriority Priority { get; set; } = DealTaskPriority.Medium;
    public DealTaskStatus Status { get; set; } = DealTaskStatus.Pending;
}
