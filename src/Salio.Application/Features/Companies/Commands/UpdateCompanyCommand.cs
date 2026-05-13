using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;

namespace Salio.Application.Features.Companies.Commands;

public record UpdateCompanyCommand(
    Guid Id,
    string Name,
    string? TaxCode,
    string? Industry,
    string? Size,
    string? Website,
    string? Phone,
    string? Email,
    string? Address,
    Guid? OwnerId) : IRequest<Unit>;

public class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class UpdateCompanyCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<UpdateCompanyCommand, Unit>
{
    public async Task<Unit> Handle(UpdateCompanyCommand cmd, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var company = await db.Companies.FirstOrDefaultAsync(
            c => c.Id == cmd.Id && c.OrgId == current.OrgId && c.DeletedAt == null, ct)
            ?? throw new NotFoundException("Company", cmd.Id);

        company.Name = cmd.Name.Trim();
        company.TaxCode = cmd.TaxCode?.Trim();
        company.Industry = cmd.Industry?.Trim();
        company.Size = cmd.Size?.Trim();
        company.Website = cmd.Website?.Trim();
        company.Phone = cmd.Phone?.Trim();
        company.Email = cmd.Email?.Trim();
        company.Address = cmd.Address?.Trim();
        company.OwnerId = cmd.OwnerId;

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
