namespace Salio.Application.Common.Interfaces;

/// <summary>
/// Lấy thông tin user đang đăng nhập trong request hiện tại.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? OrgId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Roles { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}

public interface IJwtTokenService
{
    /// <returns>Cặp access token (JWT) + refresh token (raw string) — caller hash trước khi lưu DB.</returns>
    (string AccessToken, string RefreshToken, DateTimeOffset AccessExpiresAt, DateTimeOffset RefreshExpiresAt) GenerateTokens(
        Guid userId, Guid orgId, string email, IEnumerable<string> roles);

    string HashRefreshToken(string rawToken);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(Guid userId, Guid orgId, string functionCode, string actionCode, CancellationToken ct = default);
}

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
