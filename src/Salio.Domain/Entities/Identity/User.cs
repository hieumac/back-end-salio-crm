using Salio.Domain.Common;
using Salio.Domain.Entities.Auth;
using Salio.Domain.Entities.Crm;
using Salio.Domain.Entities.Rbac;

namespace Salio.Domain.Entities.Identity;

/// <summary>
/// Người dùng — global account, có thể join nhiều tổ chức qua OrgMember.
/// Mật khẩu chuyển sang bảng AuthIdentity (provider=Password).
/// </summary>
public class User : SoftDeletableEntity
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    // IsActive đã inherit từ AuditableEntity (base field)
    public bool EmailVerified { get; set; }

    public ICollection<OrgMember> Memberships { get; set; } = [];
    public ICollection<AuthIdentity> AuthIdentities { get; set; } = [];
    public ICollection<UserSession> Sessions { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<MfaFactor> MfaFactors { get; set; } = [];
    public ICollection<LoginAttempt> LoginAttempts { get; set; } = [];
    public ICollection<ApiKey> CreatedApiKeys { get; set; } = [];
    public ICollection<Invitation> InvitationsSent { get; set; } = [];
    public ICollection<Invitation> InvitationsAccepted { get; set; } = [];
    public ICollection<Role> CreatedRoles { get; set; } = [];
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public ICollection<UserRole> AssignedUserRoles { get; set; } = [];
    public ICollection<PermissionGrant> PermissionGrants { get; set; } = [];
    public ICollection<PermissionGrant> GrantedPermissions { get; set; } = [];
    public ICollection<Team> ManagedTeams { get; set; } = [];
    public ICollection<TeamMember> TeamMemberships { get; set; } = [];
    public ICollection<Deal> AssignedDeals { get; set; } = [];
    public ICollection<Models.Task> AssignedTasks { get; set; } = [];
}
