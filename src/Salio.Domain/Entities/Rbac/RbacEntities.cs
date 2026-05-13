using Salio.Domain.Common;
using Salio.Domain.Enums;
using Salio.Domain.Entities.Identity;

namespace Salio.Domain.Entities.Rbac;

/// <summary>
/// Chức năng (UI feature) của hệ thống — vd: crm.deals.kanban, ai.chat, settings.users
/// </summary>
public class SystemFunction : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public SystemModuleGroup ModuleGroup { get; set; }
    public string? Path { get; set; }
    public string? Icon { get; set; }
    public FunctionRiskLevel RiskLevel { get; set; } = FunctionRiskLevel.Low;
    public bool IsActive { get; set; } = true;
    public int Order { get; set; }

    public ICollection<FunctionAction> FunctionActions { get; set; } = [];
    public ICollection<Permission> Permissions { get; set; } = [];
}

/// <summary>
/// Hành động chuẩn — view, create, update, delete, export, ...
/// </summary>
public class SystemAction : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }

    public ICollection<FunctionAction> FunctionActions { get; set; } = [];
    public ICollection<Permission> Permissions { get; set; } = [];
}

/// <summary>
/// Ma trận function × action — quy định những hành động nào được phép trên function nào.
/// </summary>
public class FunctionAction : BaseEntity
{
    public Guid FunctionId { get; set; }
    public SystemFunction? Function { get; set; }

    public Guid ActionId { get; set; }
    public SystemAction? Action { get; set; }

    public bool IsDefault { get; set; }
}

/// <summary>
/// Permission = function + action + scope. Auto-gen từ FunctionAction.
/// </summary>
public class Permission : AuditableEntity
{
    public Guid FunctionId { get; set; }
    public SystemFunction? Function { get; set; }

    public Guid ActionId { get; set; }
    public SystemAction? Action { get; set; }

    public PermissionScope Scope { get; set; } = PermissionScope.Any;
    public string Code { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
    public ICollection<PermissionGrant> Grants { get; set; } = [];
}

/// <summary>
/// Vai trò — có thể là system role (Owner/Admin/Sales/...) hoặc custom theo tổ chức.
/// </summary>
public class Role : AuditableEntity
{
    public Guid? OrgId { get; set; }
    public Organization? Organization { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public Guid? ParentRoleId { get; set; }
    public Role? ParentRole { get; set; }
    public int Priority { get; set; }
    public Guid? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<Role> ChildRoles { get; set; } = [];
}

public class RolePermission : BaseEntity
{
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
    public Guid PermissionId { get; set; }
    public Permission? Permission { get; set; }
}

/// <summary>
/// Gán role cho user trong context một tổ chức.
/// </summary>
public class UserRole : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid OrgId { get; set; }
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
    public Guid? AssignedById { get; set; }
    public User? AssignedBy { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

/// <summary>
/// Grant trực tiếp permission cho user (allow/deny override).
/// </summary>
public class PermissionGrant : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid OrgId { get; set; }
    public Guid PermissionId { get; set; }
    public Permission? Permission { get; set; }
    public GrantEffect Effect { get; set; } = GrantEffect.Allow;
    public string? Reason { get; set; }
    public Guid? GrantedById { get; set; }
    public User? GrantedBy { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}

/// <summary>
/// Đội/Team trong tổ chức — hỗ trợ phân quyền scope=team.
/// </summary>
public class Team : AuditableEntity
{
    public Guid OrgId { get; set; }
    public Organization? Organization { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public Guid? ManagerId { get; set; }
    public User? Manager { get; set; }
    public Guid? ParentTeamId { get; set; }
    public Team? ParentTeam { get; set; }

    public ICollection<TeamMember> Members { get; set; } = [];
    public ICollection<Team> ChildTeams { get; set; } = [];
}

public class TeamMember : AuditableEntity
{
    public Guid TeamId { get; set; }
    public Team? Team { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public TeamRoleType RoleType { get; set; } = TeamRoleType.Member;
}
