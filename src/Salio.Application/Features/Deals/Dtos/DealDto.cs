namespace Salio.Application.Features.Deals.Dtos;

public record DealDto(
    Guid Id,
    string Code,
    string Title,
    decimal Value,
    string Currency,
    int Probability,
    Guid PipelineId,
    Guid StageId,
    string? StageName,
    Guid? CompanyId,
    string? CompanyName,
    Guid? ContactId,
    string? ContactName,
    Guid? AssigneeId,
    string? AssigneeName,
    DateOnly? ExpectedCloseDate,
    int? AiScore,
    DateTimeOffset? LastActivityAt,
    DateTimeOffset CreatedAt);

public record DealListItemDto(
    Guid Id,
    string Code,
    string Title,
    decimal Value,
    string Currency,
    Guid StageId,
    string? StageName,
    string? CompanyName,
    int? AiScore,
    DateOnly? ExpectedCloseDate);
