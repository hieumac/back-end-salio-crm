using FluentValidation;
using MediatR;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;
using Salio.Domain.Entities.Crm;

namespace Salio.Application.Features.Companies.Commands;

public record CreateCompanyCommand(
    string Name,
    string? TaxCode,
    string? Industry,
    string? Size,
    string? Website,
    string? Phone,
    string? Email,
    string? Address,
    Guid? OwnerId) : IRequest<Guid>;

public class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.TaxCode).MaximumLength(50);
        RuleFor(x => x.Website).MaximumLength(255);
    }
}

public class CreateCompanyCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<CreateCompanyCommand, Guid>
{
    public async Task<Guid> Handle(CreateCompanyCommand cmd, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var company = new Company
        {
            OrgId = current.OrgId.Value,
            Name = cmd.Name.Trim(),
            TaxCode = cmd.TaxCode?.Trim(),
            Industry = cmd.Industry?.Trim(),
            Size = cmd.Size?.Trim(),
            Website = cmd.Website?.Trim(),
            Phone = cmd.Phone?.Trim(),
            Email = cmd.Email?.Trim(),
            Address = cmd.Address?.Trim(),
            OwnerId = cmd.OwnerId ?? current.UserId,
        };

        db.Companies.Add(company);
        await db.SaveChangesAsync(ct);
        return company.Id;
    }
}
