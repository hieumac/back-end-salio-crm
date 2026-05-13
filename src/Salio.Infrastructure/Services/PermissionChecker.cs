using Microsoft.EntityFrameworkCore;
using Salio.Application.Common.Interfaces;
using Salio.Domain.Enums;
using Salio.Infrastructure.Persistence;

namespace Salio.Infrastructure.Services;

/// <summary>
/// Kiểm tra user có quyền function.action không, đi qua:
///  1. PermissionGrant deny → false
///  2. PermissionGrant allow → true
///  3. UserRole → RolePermission → true
/// </summary>
public class PermissionChecker(SalioDbContext db) : IPermissionChecker
{
    public async Task<bool> HasPermissionAsync(Guid userId, Guid orgId, string functionCode, string actionCode, CancellationToken ct = default)
    {
        var permIds = await db.Permissions
            .Where(p => p.Function!.Code == functionCode && p.Action!.Code == actionCode)
            .Select(p => p.Id)
            .ToListAsync(ct);

        if (permIds.Count == 0) return false;

        // Direct deny short-circuit
        var hasDeny = await db.PermissionGrants
            .AnyAsync(g => g.UserId == userId && g.OrgId == orgId
                && permIds.Contains(g.PermissionId)
                && g.Effect == GrantEffect.Deny
                && (g.ExpiresAt == null || g.ExpiresAt > DateTimeOffset.UtcNow), ct);
        if (hasDeny) return false;

        var hasAllow = await db.PermissionGrants
            .AnyAsync(g => g.UserId == userId && g.OrgId == orgId
                && permIds.Contains(g.PermissionId)
                && g.Effect == GrantEffect.Allow
                && (g.ExpiresAt == null || g.ExpiresAt > DateTimeOffset.UtcNow), ct);
        if (hasAllow) return true;

        var viaRole = await db.UserRoles
            .Where(ur => ur.UserId == userId && ur.OrgId == orgId
                && (ur.ExpiresAt == null || ur.ExpiresAt > DateTimeOffset.UtcNow))
            .SelectMany(ur => ur.Role!.RolePermissions)
            .AnyAsync(rp => permIds.Contains(rp.PermissionId), ct);

        return viaRole;
    }
}
