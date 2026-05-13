using Microsoft.EntityFrameworkCore;
using Salio.Domain.Entities.Rbac;
using Salio.Domain.Enums;

namespace Salio.Infrastructure.Persistence;

/// <summary>
/// Seed system functions + actions + permissions + system roles.
/// Idempotent — gọi nhiều lần không tạo trùng.
/// </summary>
public static class SalioDbSeeder
{
    public static async Task SeedAsync(SalioDbContext db, CancellationToken ct = default)
    {
        await SeedActionsAsync(db, ct);
        await SeedFunctionsAsync(db, ct);
        await SeedFunctionActionsAsync(db, ct);
        await SeedPermissionsAsync(db, ct);
        await SeedSystemRolesAsync(db, ct);
    }

    static readonly (string Code, string Name)[] Actions =
    [
        ("view", "Xem"),
        ("create", "Tạo mới"),
        ("update", "Cập nhật"),
        ("delete", "Xóa"),
        ("export", "Xuất dữ liệu"),
        ("import", "Nhập dữ liệu"),
        ("approve", "Phê duyệt"),
        ("assign", "Gán/Chuyển"),
        ("merge", "Gộp"),
        ("transfer", "Chuyển sở hữu"),
        ("share", "Chia sẻ"),
        ("execute", "Thực thi"),
        ("configure", "Cấu hình"),
        ("archive", "Lưu trữ"),
        ("bulk_edit", "Chỉnh sửa hàng loạt"),
    ];

    static readonly (string Code, string Name, SystemModuleGroup Group, FunctionRiskLevel Risk, string? Path)[] Functions =
    [
        ("dashboard.crm_work", "Bảng điều khiển công việc", SystemModuleGroup.Dashboard, FunctionRiskLevel.Low, "/"),
        ("dashboard.analytics", "Phân tích KPI", SystemModuleGroup.Dashboard, FunctionRiskLevel.Low, "/analytics"),

        ("crm.deals.kanban", "Pipeline Kanban", SystemModuleGroup.Crm, FunctionRiskLevel.Low, "/crm/deals/kanban"),
        ("crm.deals.list", "Danh sách Deal", SystemModuleGroup.Crm, FunctionRiskLevel.Low, "/crm/deals"),
        ("crm.deals.detail", "Chi tiết Deal", SystemModuleGroup.Crm, FunctionRiskLevel.Low, "/crm/deals/:id"),
        ("crm.companies", "Quản lý khách hàng (Công ty)", SystemModuleGroup.Crm, FunctionRiskLevel.Low, "/crm/companies"),
        ("crm.contacts", "Quản lý liên hệ", SystemModuleGroup.Crm, FunctionRiskLevel.Low, "/crm/contacts"),
        ("crm.products", "Quản lý sản phẩm", SystemModuleGroup.Crm, FunctionRiskLevel.Low, "/crm/products"),
        ("crm.pipelines", "Cấu hình Pipeline & Stage", SystemModuleGroup.Crm, FunctionRiskLevel.Medium, "/crm/pipelines"),
        ("crm.tasks", "Công việc", SystemModuleGroup.Crm, FunctionRiskLevel.Low, "/crm/tasks"),
        ("crm.duplicate_check", "Kiểm tra trùng dữ liệu", SystemModuleGroup.Crm, FunctionRiskLevel.Medium, "/crm/duplicates"),

        ("ai.chat", "Trợ lý AI Chat", SystemModuleGroup.Ai, FunctionRiskLevel.Low, "/ai/chat"),
        ("ai.insights", "AI Insights", SystemModuleGroup.Ai, FunctionRiskLevel.Low, "/ai/insights"),
        ("ai.scoring", "AI Scoring", SystemModuleGroup.Ai, FunctionRiskLevel.Low, "/ai/scoring"),

        ("library.company", "Thư viện công ty", SystemModuleGroup.Library, FunctionRiskLevel.Low, "/library/company"),
        ("library.personal", "Thư viện cá nhân", SystemModuleGroup.Library, FunctionRiskLevel.Low, "/library/personal"),
        ("library.shared", "Thư viện chia sẻ", SystemModuleGroup.Library, FunctionRiskLevel.Low, "/library/shared"),

        ("reports.sales", "Báo cáo Sales", SystemModuleGroup.Reports, FunctionRiskLevel.Low, "/reports/sales"),
        ("reports.pipeline", "Báo cáo Pipeline", SystemModuleGroup.Reports, FunctionRiskLevel.Low, "/reports/pipeline"),
        ("reports.team", "Báo cáo theo team", SystemModuleGroup.Reports, FunctionRiskLevel.Low, "/reports/team"),

        ("settings.organization", "Thông tin tổ chức", SystemModuleGroup.Settings, FunctionRiskLevel.Medium, "/settings/organization"),
        ("settings.users", "Quản lý người dùng", SystemModuleGroup.Settings, FunctionRiskLevel.High, "/settings/users"),
        ("settings.roles", "Quản lý vai trò & quyền", SystemModuleGroup.Settings, FunctionRiskLevel.Critical, "/settings/roles"),
        ("settings.teams", "Quản lý team", SystemModuleGroup.Settings, FunctionRiskLevel.Medium, "/settings/teams"),
        ("settings.billing", "Thanh toán & gói cước", SystemModuleGroup.Settings, FunctionRiskLevel.High, "/settings/billing"),
        ("settings.audit", "Audit log", SystemModuleGroup.Settings, FunctionRiskLevel.High, "/settings/audit"),
        ("settings.security", "Cấu hình bảo mật", SystemModuleGroup.Settings, FunctionRiskLevel.Critical, "/settings/security"),
        ("settings.integrations", "Tích hợp bên thứ ba", SystemModuleGroup.Settings, FunctionRiskLevel.Medium, "/settings/integrations"),
        ("settings.api_keys", "API Keys", SystemModuleGroup.Settings, FunctionRiskLevel.High, "/settings/api-keys"),

        ("system.tenants", "Quản lý tenant (super admin)", SystemModuleGroup.System, FunctionRiskLevel.Critical, "/system/tenants"),
        ("system.functions", "Quản lý function & action", SystemModuleGroup.System, FunctionRiskLevel.Critical, "/system/functions"),
    ];

    /// <summary>
    /// Map function → default actions cho phép.
    /// Convention: tất cả entity CRM hỗ trợ full CRUD + export. Settings/system → có thêm configure.
    /// </summary>
    static readonly Dictionary<string, string[]> FunctionDefaultActions = new()
    {
        ["dashboard.crm_work"] = ["view"],
        ["dashboard.analytics"] = ["view", "export"],

        ["crm.deals.kanban"] = ["view"],
        ["crm.deals.list"] = ["view", "create", "update", "delete", "export", "import", "assign", "transfer", "bulk_edit"],
        ["crm.deals.detail"] = ["view", "update", "share"],
        ["crm.companies"] = ["view", "create", "update", "delete", "export", "import", "merge", "transfer", "bulk_edit"],
        ["crm.contacts"] = ["view", "create", "update", "delete", "export", "import", "merge", "bulk_edit"],
        ["crm.products"] = ["view", "create", "update", "delete", "export", "import"],
        ["crm.pipelines"] = ["view", "create", "update", "delete", "configure"],
        ["crm.tasks"] = ["view", "create", "update", "delete", "assign"],
        ["crm.duplicate_check"] = ["view", "execute", "merge"],

        ["ai.chat"] = ["view", "execute"],
        ["ai.insights"] = ["view", "execute"],
        ["ai.scoring"] = ["view", "execute", "configure"],

        ["library.company"] = ["view", "create", "update", "delete", "share", "archive"],
        ["library.personal"] = ["view", "create", "update", "delete", "share"],
        ["library.shared"] = ["view", "create", "update", "delete", "share"],

        ["reports.sales"] = ["view", "export"],
        ["reports.pipeline"] = ["view", "export"],
        ["reports.team"] = ["view", "export"],

        ["settings.organization"] = ["view", "update", "configure"],
        ["settings.users"] = ["view", "create", "update", "delete", "assign"],
        ["settings.roles"] = ["view", "create", "update", "delete", "configure"],
        ["settings.teams"] = ["view", "create", "update", "delete", "assign"],
        ["settings.billing"] = ["view", "configure"],
        ["settings.audit"] = ["view", "export"],
        ["settings.security"] = ["view", "configure"],
        ["settings.integrations"] = ["view", "configure"],
        ["settings.api_keys"] = ["view", "create", "delete"],

        ["system.tenants"] = ["view", "create", "update", "delete", "configure"],
        ["system.functions"] = ["view", "configure"],
    };

    private static async Task SeedActionsAsync(SalioDbContext db, CancellationToken ct)
    {
        var existing = await db.SystemActions.Select(a => a.Code).ToListAsync(ct);
        var i = 0;
        foreach (var (code, name) in Actions)
        {
            if (!existing.Contains(code))
                db.SystemActions.Add(new SystemAction { Code = code, Name = name, Order = i });
            i++;
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedFunctionsAsync(SalioDbContext db, CancellationToken ct)
    {
        var existing = await db.SystemFunctions.Select(f => f.Code).ToListAsync(ct);
        var i = 0;
        foreach (var (code, name, group, risk, path) in Functions)
        {
            if (!existing.Contains(code))
                db.SystemFunctions.Add(new SystemFunction
                {
                    Code = code, Name = name, ModuleGroup = group,
                    RiskLevel = risk, Path = path, Order = i, IsActive = true,
                });
            i++;
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedFunctionActionsAsync(SalioDbContext db, CancellationToken ct)
    {
        var allFns = await db.SystemFunctions.ToListAsync(ct);
        var allActs = await db.SystemActions.ToListAsync(ct);
        var existing = await db.FunctionActions.Select(fa => new { fa.FunctionId, fa.ActionId }).ToListAsync(ct);

        foreach (var fn in allFns)
        {
            if (!FunctionDefaultActions.TryGetValue(fn.Code, out var allowed)) continue;
            foreach (var actCode in allowed)
            {
                var act = allActs.FirstOrDefault(a => a.Code == actCode);
                if (act == null) continue;
                if (existing.Any(e => e.FunctionId == fn.Id && e.ActionId == act.Id)) continue;
                db.FunctionActions.Add(new FunctionAction
                {
                    FunctionId = fn.Id, ActionId = act.Id, IsDefault = true,
                });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedPermissionsAsync(SalioDbContext db, CancellationToken ct)
    {
        var funcActions = await db.FunctionActions
            .Include(fa => fa.Function).Include(fa => fa.Action)
            .ToListAsync(ct);
        var existing = await db.Permissions
            .Select(p => new { p.FunctionId, p.ActionId, p.Scope }).ToListAsync(ct);

        foreach (var fa in funcActions)
        {
            // Tạo 1 permission scope=any cho mỗi function-action
            if (existing.Any(e => e.FunctionId == fa.FunctionId && e.ActionId == fa.ActionId && e.Scope == PermissionScope.Any))
                continue;

            db.Permissions.Add(new Permission
            {
                FunctionId = fa.FunctionId,
                ActionId = fa.ActionId,
                Scope = PermissionScope.Any,
                Code = $"{fa.Function!.Code}:{fa.Action!.Code}:any",
            });
        }
        await db.SaveChangesAsync(ct);
    }

    static readonly (string Code, string Name, int Priority, string[] Scopes)[] SystemRoles =
    [
        ("super_admin", "Super Admin (Salio team)", 100, ["*"]),
        ("owner", "Chủ tổ chức", 90, ["*"]),
        ("admin", "Quản trị viên", 80, ["dashboard.*", "crm.*", "ai.*", "library.*", "reports.*", "settings.users", "settings.roles", "settings.teams", "settings.audit", "settings.integrations"]),
        ("manager", "Quản lý Sales", 60, ["dashboard.*", "crm.*", "ai.*", "library.*", "reports.*"]),
        ("sales", "Nhân viên Sales", 40, ["dashboard.crm_work", "crm.deals.*", "crm.companies", "crm.contacts", "crm.products:view", "crm.tasks", "ai.*", "library.personal", "library.shared:view", "library.company:view", "reports.sales:view"]),
        ("viewer", "Người xem", 20, ["dashboard.crm_work", "crm.deals.*:view", "crm.companies:view", "crm.contacts:view", "ai.chat:view"]),
    ];

    private static async Task SeedSystemRolesAsync(SalioDbContext db, CancellationToken ct)
    {
        var allPerms = await db.Permissions
            .Include(p => p.Function).Include(p => p.Action)
            .ToListAsync(ct);

        foreach (var (code, name, priority, scopes) in SystemRoles)
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Code == code && r.IsSystem && r.OrgId == null, ct);
            if (role == null)
            {
                role = new Role
                {
                    Code = code, Name = name, IsSystem = true, OrgId = null, Priority = priority,
                };
                db.Roles.Add(role);
                await db.SaveChangesAsync(ct);
            }

            var rolePermIds = await db.RolePermissions.Where(rp => rp.RoleId == role.Id).Select(rp => rp.PermissionId).ToListAsync(ct);

            foreach (var p in allPerms)
            {
                if (rolePermIds.Contains(p.Id)) continue;
                if (Matches(scopes, p.Function!.Code, p.Action!.Code))
                {
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = p.Id });
                }
            }
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Match scope expression "module.*", "fn.*:action", "fn:action", "*" against permission.
    /// </summary>
    private static bool Matches(string[] scopes, string functionCode, string actionCode)
    {
        foreach (var s in scopes)
        {
            if (s == "*") return true;
            var parts = s.Split(':', 2);
            var fnPattern = parts[0];
            var actPattern = parts.Length > 1 ? parts[1] : "*";

            bool fnMatch = fnPattern.EndsWith(".*")
                ? functionCode.StartsWith(fnPattern[..^1])
                : functionCode == fnPattern;
            bool actMatch = actPattern == "*" || actPattern == actionCode;

            if (fnMatch && actMatch) return true;
        }
        return false;
    }
}
