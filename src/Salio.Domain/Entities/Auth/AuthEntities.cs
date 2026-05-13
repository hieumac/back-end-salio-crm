using Salio.Domain.Common;
using Salio.Domain.Enums;
using Salio.Domain.Entities.Identity;

namespace Salio.Domain.Entities.Auth;

/// <summary>
/// Một danh tính xác thực (password / Google / Microsoft / SAML / Apple ...).
/// Một user có thể có nhiều identity. PasswordHash chỉ dùng khi Provider=Password.
/// </summary>
public class AuthIdentity : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public AuthProvider Provider { get; set; }
    public string? ProviderUserId { get; set; }
    public string? PasswordHash { get; set; }
    public DateTimeOffset? PasswordChangedAt { get; set; }
    public string? ProviderMetadata { get; set; }  // jsonb
    public DateTimeOffset? LastUsedAt { get; set; }
}

/// <summary>
/// Phiên đăng nhập của user — gắn với thiết bị/IP.
/// </summary>
public class UserSession : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public string SessionToken { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceFingerprint { get; set; }
    public string? DeviceName { get; set; }
    public DateTimeOffset? LastActiveAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

/// <summary>
/// Refresh token rotation — mỗi session có một chuỗi refresh tokens.
/// </summary>
public class RefreshToken : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid? SessionId { get; set; }
    public UserSession? Session { get; set; }

    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
}

public class EmailVerificationToken : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
}

public class PasswordResetToken : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public string? IpAddress { get; set; }
}

public class MfaFactor : AuditableEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public MfaType Type { get; set; }
    public string? SecretEncrypted { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Label { get; set; }
    public bool IsPrimary { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }

    public ICollection<MfaChallenge> Challenges { get; set; } = [];
}

public class MfaChallenge : AuditableEntity
{
    public Guid FactorId { get; set; }
    public MfaFactor? Factor { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public int Attempts { get; set; }
}

public class LoginAttempt : AuditableEntity
{
    public string Email { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public LoginResult Result { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? FailureReason { get; set; }
}

public class ApiKey : AuditableEntity
{
    public Guid OrgId { get; set; }
    public Guid CreatedById { get; set; }
    public User? CreatedBy { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string? Scopes { get; set; }  // jsonb
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public class Invitation : AuditableEntity
{
    public Guid OrgId { get; set; }
    public Guid InvitedById { get; set; }
    public User? InvitedBy { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public User? AcceptedByUser { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string? RoleCode { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
