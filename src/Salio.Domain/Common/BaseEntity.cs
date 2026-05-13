namespace Salio.Domain.Common;

/// <summary>
/// Base entity với khóa chính UUID.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

/// <summary>
/// Entity có audit timestamps (created_at, updated_at).
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Entity có soft delete.
/// </summary>
public abstract class SoftDeletableEntity : AuditableEntity
{
    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsDeleted => DeletedAt.HasValue;
}

/// <summary>
/// Entity thuộc về tổ chức (multi-tenant). Mọi truy vấn phải lọc theo OrgId.
/// </summary>
public abstract class TenantEntity : SoftDeletableEntity
{
    public Guid OrgId { get; set; }
}
