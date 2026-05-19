using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salio.Domain.Common;

// ──────────────────────────────────────────────────────────────────────────────
// Base Fields System (10 trường chuẩn cho mọi bảng):
//   1.  id          — PK UUID
//   2.  created_at  — Thời điểm tạo
//   3.  created_by  — User tạo
//   4.  updated_at  — Thời điểm cập nhật gần nhất
//   5.  updated_by  — User cập nhật gần nhất
//   6.  deleted_at  — Thời điểm xóa mềm (chỉ SoftDeletableEntity)
//   7.  deleted_by  — User xóa mềm (chỉ SoftDeletableEntity)
//   8.  is_active   — Bật/tắt trạng thái
//   9.  sort_index  — Thứ tự sắp xếp UI
//   10. version     — Optimistic locking (xmin của PostgreSQL)
//
// Hierarchy:
//   BaseEntity (Id only — dùng cho junction table có composite key)
//     └─ AuditableEntity (Id + 8 audit/control fields, KHÔNG có soft delete)
//          └─ SoftDeletableEntity (+ DeletedAt + DeletedBy)
//               └─ TenantEntity (+ OrgId — multi-tenant)
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Base entity tối thiểu — chỉ có khóa chính UUID.
/// Dùng cho junction table có composite key (không cần audit).
/// </summary>
public abstract class BaseEntity
{
    /// <summary>Khóa chính UUID.</summary>
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();
}

/// <summary>
/// Entity có audit + control fields chuẩn (8 trường ngoài Id).
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    /// <summary>Thời điểm tạo bản ghi (UTC).</summary>
    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UserId người tạo bản ghi (nullable cho seed / system).</summary>
    [Column("created_by")]
    public Guid? CreatedBy { get; set; }

    /// <summary>Thời điểm cập nhật gần nhất (UTC). Tự cập nhật trong SaveChangesAsync.</summary>
    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UserId người cập nhật gần nhất.</summary>
    [Column("updated_by")]
    public Guid? UpdatedBy { get; set; }

    /// <summary>Cờ bật/tắt trạng thái hoạt động (không phải soft-delete). Mặc định true.</summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>Thứ tự sắp xếp UI (drag &amp; drop). Mặc định 0.</summary>
    [Column("sort_index")]
    public int SortIndex { get; set; }

    /// <summary>
    /// Optimistic concurrency token — map sang cột hệ thống <c>xmin</c> của PostgreSQL.
    /// Tự tăng theo từng transaction; SaveChanges sẽ throw <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
    /// nếu giá trị này không khớp giữa lúc đọc và lúc UPDATE.
    /// </summary>
    [Timestamp]
    [Column("xmin", TypeName = "xid")]
    public uint Version { get; set; }
}

/// <summary>
/// Entity hỗ trợ soft delete — không thực sự xóa khỏi DB, chỉ đánh dấu DeletedAt.
/// </summary>
public abstract class SoftDeletableEntity : AuditableEntity
{
    /// <summary>Thời điểm xóa mềm. NULL = bản ghi còn hiệu lực.</summary>
    [Column("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>UserId người thực hiện xóa mềm.</summary>
    [Column("deleted_by")]
    public Guid? DeletedBy { get; set; }

    /// <summary>Computed — true nếu bản ghi đã bị xóa mềm.</summary>
    [NotMapped]
    public bool IsDeleted => DeletedAt.HasValue;
}

/// <summary>
/// Entity thuộc về một tổ chức (multi-tenant). Mọi truy vấn phải lọc theo OrgId.
/// </summary>
public abstract class TenantEntity : SoftDeletableEntity
{
    /// <summary>OrgId — khóa ngoại tới <c>organizations.id</c>.</summary>
    [Column("org_id")]
    public Guid OrgId { get; set; }
}
