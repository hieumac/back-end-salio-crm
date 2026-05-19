using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;
using Salio.Domain.Entities.Auth;
using Salio.Domain.Entities.Identity;
using Salio.Domain.Entities.Rbac;
using Salio.Domain.Enums;

namespace Salio.Application.Features.Auth.Commands;

public record RegisterCommand(string Email, string Password, string FullName, string OrgName) : IRequest<Guid>;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.OrgName).NotEmpty().MaximumLength(200);
    }
}

public class RegisterCommandHandler(
    ISalioDbContext db,
    IPasswordHasher hasher) : IRequestHandler<RegisterCommand, Guid>
{
    public async Task<Guid> Handle(RegisterCommand request, CancellationToken ct)
    {
        var exists = await db.Users.AnyAsync(u => u.Email == request.Email, ct);
        if (exists) throw new ConflictException("Email already registered");

        var org = new Organization
        {
            Name = request.OrgName,
            Slug = Slugify(request.OrgName),
            Plan = "free",
        };
        db.Organizations.Add(org);

        var user = new User
        {
            Email = request.Email,
            FullName = request.FullName,
            EmailVerified = false,
        };
        db.Users.Add(user);

        db.AuthIdentities.Add(new AuthIdentity
        {
            UserId = user.Id,
            Provider = AuthProvider.Password,
            PasswordHash = hasher.Hash(request.Password),
            PasswordChangedAt = DateTimeOffset.UtcNow,
        });

        db.OrgMembers.Add(new OrgMember
        {
            UserId = user.Id,
            OrgId = org.Id,
            Title = "Founder",
            IsActive = true,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        // Assign owner role nếu có
        var ownerRole = await db.Roles.FirstOrDefaultAsync(r => r.Code == "owner" && r.IsSystem, ct);
        if (ownerRole is not null)
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                OrgId = org.Id,
                RoleId = ownerRole.Id,
            });
        }

        await db.SaveChangesAsync(ct);
        return user.Id;
    }

    private static string Slugify(string input) =>
        new string(input.ToLowerInvariant()
            .Replace(' ', '-')
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .ToArray());
}
