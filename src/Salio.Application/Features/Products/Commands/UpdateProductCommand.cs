using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;

namespace Salio.Application.Features.Products.Commands;

public record UpdateProductCommand(
    Guid Id,
    string Name,
    string? Description,
    decimal UnitPrice,
    string Unit,
    string Currency,
    bool IsActive) : IRequest<Unit>;

public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(255);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}

public class UpdateProductCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<UpdateProductCommand, Unit>
{
    public async Task<Unit> Handle(UpdateProductCommand cmd, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var product = await db.Products.FirstOrDefaultAsync(
            p => p.Id == cmd.Id && p.OrgId == current.OrgId && p.DeletedAt == null, ct)
            ?? throw new NotFoundException("Product", cmd.Id);

        product.Name = cmd.Name.Trim();
        product.Description = cmd.Description?.Trim();
        product.UnitPrice = cmd.UnitPrice;
        product.Unit = cmd.Unit;
        product.Currency = cmd.Currency.ToUpperInvariant();
        product.IsActive = cmd.IsActive;

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
