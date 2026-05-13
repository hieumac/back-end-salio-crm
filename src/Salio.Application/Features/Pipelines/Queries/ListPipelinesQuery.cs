using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Application.Features.Pipelines.Dtos;
using Salio.Domain.Common;

namespace Salio.Application.Features.Pipelines.Queries;

public record ListPipelinesQuery() : IRequest<IReadOnlyList<PipelineDto>>;

public class ListPipelinesQueryHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<ListPipelinesQuery, IReadOnlyList<PipelineDto>>
{
    public async Task<IReadOnlyList<PipelineDto>> Handle(ListPipelinesQuery q, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var pipelines = await db.Pipelines.AsNoTracking()
            .Where(p => p.OrgId == current.OrgId && p.DeletedAt == null)
            .OrderBy(p => p.Order).ThenBy(p => p.Name)
            .Select(p => new PipelineDto(
                p.Id, p.Name, p.IsDefault, p.Order,
                p.Stages.OrderBy(s => s.Order).Select(s => new PipelineStageDto(
                    s.Id, s.Code, s.Name, s.Order, s.DefaultProbability,
                    s.IsWon, s.IsLost, s.Color,
                    s.Deals.Count(d => d.DeletedAt == null)))
                    .ToList()))
            .ToListAsync(ct);

        return pipelines;
    }
}
