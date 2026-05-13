using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;
using Salio.Domain.Entities.Crm;

namespace Salio.Application.Features.Pipelines.Commands;

public record CreatePipelineCommand(
    string Name,
    bool IsDefault,
    IReadOnlyList<StageInput> Stages) : IRequest<Guid>;

public record StageInput(string Code, string Name, int Order, int DefaultProbability, bool IsWon, bool IsLost, string? Color);

public class CreatePipelineCommandValidator : AbstractValidator<CreatePipelineCommand>
{
    public CreatePipelineCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Stages).NotEmpty().WithMessage("Pipeline cần có ít nhất 1 stage");
        RuleForEach(x => x.Stages).ChildRules(s =>
        {
            s.RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
            s.RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            s.RuleFor(x => x.DefaultProbability).InclusiveBetween(0, 100);
        });
    }
}

public class CreatePipelineCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<CreatePipelineCommand, Guid>
{
    public async Task<Guid> Handle(CreatePipelineCommand cmd, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        // Nếu đặt làm default, hạ các pipeline default khác
        if (cmd.IsDefault)
        {
            var existing = await db.Pipelines
                .Where(p => p.OrgId == current.OrgId && p.IsDefault && p.DeletedAt == null)
                .ToListAsync(ct);
            foreach (var p in existing) p.IsDefault = false;
        }

        var maxOrder = await db.Pipelines
            .Where(p => p.OrgId == current.OrgId && p.DeletedAt == null)
            .MaxAsync(p => (int?)p.Order, ct) ?? 0;

        var pipeline = new Pipeline
        {
            OrgId = current.OrgId.Value,
            Name = cmd.Name.Trim(),
            IsDefault = cmd.IsDefault,
            Order = maxOrder + 1,
            Stages = cmd.Stages.Select(s => new PipelineStage
            {
                Code = s.Code.Trim(),
                Name = s.Name.Trim(),
                Order = s.Order,
                DefaultProbability = s.DefaultProbability,
                IsWon = s.IsWon,
                IsLost = s.IsLost,
                Color = s.Color,
            }).ToList(),
        };

        db.Pipelines.Add(pipeline);
        await db.SaveChangesAsync(ct);
        return pipeline.Id;
    }
}
