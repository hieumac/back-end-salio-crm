using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Application.Features.Auth.Commands;
using Salio.Domain.Common;
using Salio.Domain.Entities.Auth;

namespace Salio.Application.Features.Auth.Commands;

public record RefreshTokenCommand(string RefreshToken) : IRequest<LoginResponse>;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}

public class RefreshTokenCommandHandler(
    ISalioDbContext db,
    IJwtTokenService jwt) : IRequestHandler<RefreshTokenCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var hash = jwt.HashRefreshToken(request.RefreshToken);
        var token = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null, ct)
            ?? throw new NotFoundException("Refresh token");

        if (token.ExpiresAt < DateTimeOffset.UtcNow) throw new ForbiddenException("Refresh token expired");

        var user = token.User ?? throw new NotFoundException("User");

        var membership = await db.OrgMembers
            .Where(m => m.UserId == user.Id && m.IsActive)
            .FirstOrDefaultAsync(ct)
            ?? throw new ForbiddenException("No membership");

        var roles = await db.UserRoles
            .Where(ur => ur.UserId == user.Id && ur.OrgId == membership.OrgId)
            .Select(ur => ur.Role!.Code)
            .ToListAsync(ct);

        var (access, refresh, accessExp, refreshExp) = jwt.GenerateTokens(user.Id, membership.OrgId, user.Email, roles);

        // Rotation: revoke old token, create new one chained
        token.RevokedAt = DateTimeOffset.UtcNow;
        var newToken = new RefreshToken
        {
            UserId = user.Id,
            SessionId = token.SessionId,
            TokenHash = jwt.HashRefreshToken(refresh),
            ExpiresAt = refreshExp,
        };
        db.RefreshTokens.Add(newToken);
        token.ReplacedByTokenId = newToken.Id;

        await db.SaveChangesAsync(ct);

        return new LoginResponse(access, refresh, accessExp, user.Id, membership.OrgId, user.Email, user.FullName, roles);
    }
}
