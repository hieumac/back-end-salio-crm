namespace Salio.Application.Features.Contacts.Dtos;

public record ContactDto(
    Guid Id,
    Guid? CompanyId,
    string? CompanyName,
    string FullName,
    string? Email,
    string? Phone,
    string? Title,
    bool IsPrimary,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record ContactListItemDto(
    Guid Id,
    string FullName,
    string? Email,
    string? Phone,
    string? Title,
    Guid? CompanyId,
    string? CompanyName,
    bool IsPrimary);
