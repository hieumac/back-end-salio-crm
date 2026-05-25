using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Common;
using Salio.Domain.Entities.Auth;
using Salio.Domain.Enums;

namespace Salio.Application.Features.Auth.Commands;

public record LoginCommand(string Email, string Password, string? OrgSlug) : IRequest<LoginResponse>;

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessExpiresAt,
    Guid UserId,
    Guid OrgId,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles);

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
    }
}

public class LoginCommandHandler(
    ISalioDbContext db,
    IPasswordHasher hasher,
    IJwtTokenService jwt,
    ICurrentUserService current) : IRequestHandler<LoginCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email && u.DeletedAt == null, ct)
            ?? throw new NotFoundException("User", request.Email);

        if (!user.IsActive) throw new ForbiddenException("Account disabled");

        var identity = await db.AuthIdentities
            .FirstOrDefaultAsync(i => i.UserId == user.Id && i.Provider == AuthProvider.Password, ct)
            ?? throw new NotFoundException("Password identity");

        if (identity.PasswordHash is null || !hasher.Verify(request.Password, identity.PasswordHash))
        {
            db.LoginAttempts.Add(new LoginAttempt
            {
                Email = request.Email,
                UserId = user.Id,
                Result = LoginResult.InvalidCredentials,
                IpAddress = current.IpAddress,
                UserAgent = current.UserAgent,
            });
            await db.SaveChangesAsync(ct);
            throw new ForbiddenException("Invalid credentials");
        }

        // Lấy org đầu tiên user join (có thể nhận OrgSlug để chọn cụ thể)
        var membership = await db.OrgMembers
            .Include(m => m.Organization)
            .Where(m => m.UserId == user.Id && m.IsActive)
            .Where(m => request.OrgSlug == null || m.Organization!.Slug == request.OrgSlug)
            .FirstOrDefaultAsync(ct)
            ?? throw new ForbiddenException("No organization membership");

        var roles = await db.UserRoles
            .Where(ur => ur.UserId == user.Id && ur.OrgId == membership.OrgId)
            .Select(ur => ur.Role!.Code)
            .ToListAsync(ct);

        var (access, refresh, accessExp, refreshExp) = jwt.GenerateTokens(user.Id, membership.OrgId, user.Email, roles);

        var session = new UserSession
        {
            UserId = user.Id,
            SessionToken = Guid.NewGuid().ToString("N"),
            IpAddress = current.IpAddress,
            UserAgent = current.UserAgent,
            ExpiresAt = refreshExp,
            LastActiveAt = DateTimeOffset.UtcNow,
        };
        db.UserSessions.Add(session);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            SessionId = session.Id,
            TokenHash = jwt.HashRefreshToken(refresh),
            ExpiresAt = refreshExp,
        });

        db.LoginAttempts.Add(new LoginAttempt
        {
            Email = request.Email,
            UserId = user.Id,
            Result = LoginResult.Success,
            IpAddress = current.IpAddress,
            UserAgent = current.UserAgent,
        });

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return new LoginResponse(access, refresh, accessExp, user.Id, membership.OrgId, user.Email, user.FullName, roles);
    }
}
