namespace Salio.Application.Features.Users.Dtos;

public record UserMeDto(
    Guid Id,
    string Email,
    string FullName,
    string? AvatarUrl,
    bool EmailVerified,
    Guid? CurrentOrgId,
    string? CurrentOrgName,
    IReadOnlyList<string> Roles,
    DateTimeOffset? LastLoginAt);

public record UserListItemDto(
    Guid Id,
    string Email,
    string FullName,
    string? AvatarUrl,
    bool IsActive,
    IReadOnlyList<string> Roles,
    DateTimeOffset? LastLoginAt);
