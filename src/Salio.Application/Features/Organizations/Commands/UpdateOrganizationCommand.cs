using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;

namespace Salio.Application.Features.Organizations.Commands;

public record UpdateOrganizationCommand(
    string Name,
    string? Locale) : IRequest<Unit>;

public class UpdateOrganizationCommandValidator : AbstractValidator<UpdateOrganizationCommand>
{
    public UpdateOrganizationCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Locale).MaximumLength(10);
    }
}

public class UpdateOrganizationCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<UpdateOrganizationCommand, Unit>
{
    public async Task<Unit> Handle(UpdateOrganizationCommand cmd, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == current.OrgId, ct)
            ?? throw new NotFoundException("Organization", current.OrgId.Value);

        org.Name = cmd.Name.Trim();
        if (!string.IsNullOrWhiteSpace(cmd.Locale)) org.Locale = cmd.Locale;

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
