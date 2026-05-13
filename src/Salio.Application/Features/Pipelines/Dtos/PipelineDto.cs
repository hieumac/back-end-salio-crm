namespace Salio.Application.Features.Pipelines.Dtos;

public record PipelineDto(
    Guid Id,
    string Name,
    bool IsDefault,
    int Order,
    IReadOnlyList<PipelineStageDto> Stages);

public record PipelineStageDto(
    Guid Id,
    string Code,
    string Name,
    int Order,
    int DefaultProbability,
    bool IsWon,
    bool IsLost,
    string? Color,
    int DealCount);
