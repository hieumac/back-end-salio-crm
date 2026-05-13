using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;
using Salio.Domain.Entities.Crm;

namespace Salio.Application.Features.Deals.Commands;

public record MoveDealStageCommand(Guid DealId, Guid ToStageId, string? Note) : IRequest<Unit>;

public class MoveDealStageCommandValidator : AbstractValidator<MoveDealStageCommand>
{
    public MoveDealStageCommandValidator()
    {
        RuleFor(x => x.DealId).NotEmpty();
        RuleFor(x => x.ToStageId).NotEmpty();
    }
}

public class MoveDealStageCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<MoveDealStageCommand, Unit>
{
    public async Task<Unit> Handle(MoveDealStageCommand req, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var deal = await db.Deals
            .FirstOrDefaultAsync(d => d.Id == req.DealId && d.OrgId == current.OrgId, ct)
            ?? throw new NotFoundException("Deal", req.DealId);

        var newStage = await db.PipelineStages
            .FirstOrDefaultAsync(s => s.Id == req.ToStageId && s.PipelineId == deal.PipelineId, ct)
            ?? throw new NotFoundException("PipelineStage", req.ToStageId);

        if (deal.StageId == req.ToStageId) return Unit.Value;

        var now = DateTimeOffset.UtcNow;
        var fromStageId = deal.StageId;

        // Tính thời gian ở stage trước
        var lastHistory = await db.DealStageHistories
            .Where(h => h.DealId == deal.Id)
            .OrderByDescending(h => h.CreatedAt)
            .FirstOrDefaultAsync(ct);
        var lastChange = lastHistory?.CreatedAt ?? deal.CreatedAt;
        var duration = (long)(now - lastChange).TotalSeconds;

        db.DealStageHistories.Add(new DealStageHistory
        {
            DealId = deal.Id,
            FromStageId = fromStageId,
            ToStageId = req.ToStageId,
            DurationInPrevStageSeconds = duration,
            ChangedById = current.UserId,
        });

        db.DealActivities.Add(new DealActivity
        {
            DealId = deal.Id,
            Type = "stage_changed",
            Title = $"Stage → {newStage.Name}",
            Description = req.Note,
            ActorId = current.UserId,
        });

        deal.StageId = req.ToStageId;
        deal.Probability = newStage.DefaultProbability;
        deal.LastActivityAt = now;
        if (newStage.IsWon || newStage.IsLost) deal.ActualCloseDate = now;

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
