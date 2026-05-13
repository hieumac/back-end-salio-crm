using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;
using Salio.Domain.Entities.Crm;

namespace Salio.Application.Features.Products.Commands;

public record CreateProductCommand(
    string Code,
    string Name,
    string? Description,
    decimal UnitPrice,
    string Unit = "unit",
    string Currency = "VND",
    bool IsActive = true) : IRequest<Guid>;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}

public class CreateProductCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<CreateProductCommand, Guid>
{
    public async Task<Guid> Handle(CreateProductCommand cmd, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var code = cmd.Code.Trim();
        var dup = await db.Products.AnyAsync(
            p => p.OrgId == current.OrgId && p.Code == code && p.DeletedAt == null, ct);
        if (dup) throw new ConflictException($"Product code '{code}' already exists");

        var product = new Product
        {
            OrgId = current.OrgId.Value,
            Code = code,
            Name = cmd.Name.Trim(),
            Description = cmd.Description?.Trim(),
            UnitPrice = cmd.UnitPrice,
            Unit = cmd.Unit,
            Currency = cmd.Currency.ToUpperInvariant(),
            IsActive = cmd.IsActive,
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return product.Id;
    }
}
