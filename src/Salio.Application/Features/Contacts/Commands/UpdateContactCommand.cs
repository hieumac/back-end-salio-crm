using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;

namespace Salio.Application.Features.Contacts.Commands;

public record UpdateContactCommand(
    Guid Id,
    string FullName,
    Guid? CompanyId,
    string? Email,
    string? Phone,
    string? Title,
    bool IsPrimary) : IRequest<Unit>;

public class UpdateContactCommandValidator : AbstractValidator<UpdateContactCommand>
{
    public UpdateContactCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class UpdateContactCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<UpdateContactCommand, Unit>
{
    public async Task<Unit> Handle(UpdateContactCommand cmd, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        var contact = await db.Contacts.FirstOrDefaultAsync(
            c => c.Id == cmd.Id && c.OrgId == current.OrgId && c.DeletedAt == null, ct)
            ?? throw new NotFoundException("Contact", cmd.Id);

        if (cmd.IsPrimary && cmd.CompanyId.HasValue)
        {
            var others = await db.Contacts
                .Where(c => c.CompanyId == cmd.CompanyId && c.Id != cmd.Id && c.IsPrimary && c.DeletedAt == null)
                .ToListAsync(ct);
            foreach (var o in others) o.IsPrimary = false;
        }

        contact.FullName = cmd.FullName.Trim();
        contact.CompanyId = cmd.CompanyId;
        contact.Email = cmd.Email?.Trim();
        contact.Phone = cmd.Phone?.Trim();
        contact.Title = cmd.Title?.Trim();
        contact.IsPrimary = cmd.IsPrimary;

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
