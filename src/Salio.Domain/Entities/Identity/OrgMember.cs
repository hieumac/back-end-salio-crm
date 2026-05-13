using Salio.Domain.Common;

namespace Salio.Domain.Entities.Identity;

/// <summary>
/// Liên kết User ↔ Organization. Role chính trong tổ chức được quản lý qua UserRole (RBAC).
/// </summary>
public class OrgMember : AuditableEntity
{
    public Guid OrgId { get; set; }
    public Organization? Organization { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string? Title { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? JoinedAt { get; set; }
}
