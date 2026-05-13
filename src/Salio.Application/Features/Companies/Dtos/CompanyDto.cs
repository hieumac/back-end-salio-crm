namespace Salio.Application.Features.Companies.Dtos;

public record CompanyDto(
    Guid Id,
    string Name,
    string? TaxCode,
    string? Industry,
    string? Size,
    string? Website,
    string? Phone,
    string? Email,
    string? Address,
    Guid? OwnerId,
    string? OwnerName,
    int DealCount,
    int ContactCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record CompanyListItemDto(
    Guid Id,
    string Name,
    string? Industry,
    string? Email,
    string? Phone,
    string? OwnerName,
    int DealCount,
    DateTimeOffset UpdatedAt);
