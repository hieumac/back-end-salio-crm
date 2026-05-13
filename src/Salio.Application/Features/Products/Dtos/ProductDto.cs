namespace Salio.Application.Features.Products.Dtos;

public record ProductDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    decimal UnitPrice,
    string Unit,
    string Currency,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record ProductListItemDto(
    Guid Id,
    string Code,
    string Name,
    decimal UnitPrice,
    string Unit,
    string Currency,
    bool IsActive);
