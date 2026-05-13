using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;
using Salio.Domain.Entities.Crm;
using Salio.Domain.Enums;

namespace Salio.Application.Features.Deals.Commands;

public record CreateDealCommand(
    string Title,
    Guid PipelineId,
    Guid StageId,
    decimal Value,
    string Currency,
    DealSource Source,
    Guid? CompanyId,
    Guid? ContactId,
    Guid? AssigneeId,
    DateOnly? ExpectedCloseDate,
    string? Notes) : IRequest<Guid>;

public class CreateDealCommandValidator : AbstractValidator<CreateDealCommand>
{
    public CreateDealCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.PipelineId).NotEmpty();
        RuleFor(x => x.StageId).NotEmpty();
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}

public class CreateDealCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<CreateDealCommand, Guid>
{
    public async Task<Guid> Handle(CreateDealCommand req, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var stage = await db.PipelineStages.FirstOrDefaultAsync(s => s.Id == req.StageId && s.PipelineId == req.PipelineId, ct)
            ?? throw new NotFoundException("PipelineStage", req.StageId);

        var deal = new Deal
        {
            OrgId = current.OrgId.Value,
            Code = await GenerateCodeAsync(current.OrgId.Value, ct),
            Title = req.Title,
            PipelineId = req.PipelineId,
            StageId = req.StageId,
            Value = req.Value,
            Currency = req.Currency,
            Probability = stage.DefaultProbability,
            Source = req.Source,
            CompanyId = req.CompanyId,
            ContactId = req.ContactId,
            AssigneeId = req.AssigneeId ?? current.UserId,
            ExpectedCloseDate = req.ExpectedCloseDate,
            Notes = req.Notes,
            LastActivityAt = DateTimeOffset.UtcNow,
        };
        db.Deals.Add(deal);

        db.DealActivities.Add(new DealActivity
        {
            DealId = deal.Id,
            Type = "deal_created",
            Title = "Deal created",
            ActorId = current.UserId,
        });

        await db.SaveChangesAsync(ct);
        return deal.Id;
    }

    private async Task<string> GenerateCodeAsync(Guid orgId, CancellationToken ct)
    {
        var count = await db.Deals.CountAsync(d => d.OrgId == orgId, ct);
        return $"DEAL-{DateTime.UtcNow:yyyyMM}-{(count + 1):D5}";
    }
}
