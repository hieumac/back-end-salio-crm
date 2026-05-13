using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;
using Salio.Domain.Entities.Crm;

namespace Salio.Application.Features.Contacts.Commands;

public record CreateContactCommand(
    string FullName,
    Guid? CompanyId,
    string? Email,
    string? Phone,
    string? Title,
    bool IsPrimary = false) : IRequest<Guid>;

public class CreateContactCommandValidator : AbstractValidator<CreateContactCommand>
{
    public CreateContactCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class CreateContactCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<CreateContactCommand, Guid>
{
    public async Task<Guid> Handle(CreateContactCommand cmd, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("No tenant context");

        if (cmd.CompanyId.HasValue)
        {
            var companyExists = await db.Companies.AnyAsync(
                c => c.Id == cmd.CompanyId && c.OrgId == current.OrgId && c.DeletedAt == null, ct);
            if (!companyExists) throw new NotFoundException("Company", cmd.CompanyId.Value);
        }

        // Nếu IsPrimary=true, hạ tất cả contact khác của cùng company xuống
        if (cmd.IsPrimary && cmd.CompanyId.HasValue)
        {
            var others = await db.Contacts
                .Where(c => c.CompanyId == cmd.CompanyId && c.IsPrimary && c.DeletedAt == null)
                .ToListAsync(ct);
            foreach (var o in others) o.IsPrimary = false;
        }

        var contact = new Contact
        {
            OrgId = current.OrgId.Value,
            CompanyId = cmd.CompanyId,
            FullName = cmd.FullName.Trim(),
            Email = cmd.Email?.Trim(),
            Phone = cmd.Phone?.Trim(),
            Title = cmd.Title?.Trim(),
            IsPrimary = cmd.IsPrimary,
        };

        db.Contacts.Add(contact);
        await db.SaveChangesAsync(ct);
        return contact.Id;
    }
}
