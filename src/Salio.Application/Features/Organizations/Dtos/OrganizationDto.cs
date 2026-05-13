namespace Salio.Application.Features.Organizations.Dtos;

public record OrganizationDto(
    Guid Id,
    string Slug,
    string Name,
    string? Plan,
    string? Locale,
    int MemberCount,
    DateTimeOffset CreatedAt);
