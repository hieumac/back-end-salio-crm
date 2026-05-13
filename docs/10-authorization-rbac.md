# 10 — Authorization & RBAC

> Function-based RBAC: **permission = function × action × scope**. Không có free-form string.

## Khái niệm

| Thuật ngữ | Ý nghĩa | Ví dụ |
|---|---|---|
| **SystemFunction** | Một màn hình/chức năng UI | `crm.deals.list`, `crm.deals.detail`, `ai.chat` |
| **SystemAction** | Hành động chuẩn lên function | `view`, `create`, `update`, `delete`, `export`, `approve`... |
| **FunctionAction** | Quy định function nào support action nào | `crm.deals.list` support `view, create, update, export` (không có `approve`) |
| **Permission** | Tổ hợp `function + action + scope` | `crm.deals.list:create:any` |
| **Role** | Tập hợp permission, gán cho user | `owner`, `admin`, `manager`, `sales`, `viewer`, `super_admin` |
| **RolePermission** | M-M giữa Role và Permission | `manager` có quyền `crm.deals.list:view:team` |
| **UserRole** | Gán role cho user trong context org | User X có role `sales` trong org Y |
| **PermissionGrant** | Grant trực tiếp lên user (override role) | Grant `crm.deals.export:Any` cho user Z, hoặc Deny `crm.deals.delete` |

## 30 System Functions

Chia theo `ModuleGroup`:

| Group | Functions |
|---|---|
| **Dashboard** | `dashboard.overview` |
| **Crm** | `crm.deals.list`, `crm.deals.detail`, `crm.deals.kanban`, `crm.deals.import`, `crm.companies`, `crm.contacts`, `crm.pipelines`, `crm.products`, `crm.tasks`, `crm.duplicates` |
| **Ai** | `ai.chat`, `ai.insights`, `ai.scoring` |
| **Library** | `library.documents`, `library.search`, `library.upload` |
| **Reports** | `reports.sales`, `reports.pipeline`, `reports.forecast`, `reports.activity` |
| **Settings** | `settings.organization`, `settings.members`, `settings.teams`, `settings.integrations`, `settings.api_keys`, `settings.notifications` |
| **System** | `system.users`, `system.roles`, `system.permissions`, `system.audit_logs` |

## 15 System Actions

| Code | Tên |
|---|---|
| `view` | Xem |
| `create` | Tạo mới |
| `update` | Cập nhật |
| `delete` | Xóa |
| `export` | Xuất file |
| `import` | Nhập từ file |
| `approve` | Phê duyệt |
| `assign` | Gán/chuyển owner |
| `merge` | Gộp |
| `transfer` | Chuyển sở hữu |
| `share` | Chia sẻ |
| `execute` | Thực thi (script/AI) |
| `configure` | Cấu hình |
| `archive` | Lưu trữ |
| `bulk_edit` | Sửa hàng loạt |

## Scope (4 mức)

| Scope | Áp dụng | Ví dụ |
|---|---|---|
| **Own** | Record do user tạo / sở hữu | Sales chỉ xem deal của họ |
| **Assigned** | Record được gán cho user (assignee) | Sales xem deal được giao |
| **Team** | Record của team user là member | Manager xem deal cả team |
| **Any** | Tất cả record trong org | Admin xem mọi deal |

> Hiện tại implementation **chỉ dùng scope=Any** (`PermissionScope.Any`). Scope khác là dự kiến mở rộng — handler sẽ filter thêm theo scope khi check.

## FunctionAction matrix

Không phải action nào cũng valid trên function nào. Bảng `FunctionDefaultActions` trong seeder:

```csharp
private static readonly Dictionary<string, string[]> FunctionDefaultActions = new()
{
    ["dashboard.overview"]   = ["view"],
    ["crm.deals.list"]       = ["view", "create", "update", "delete", "export", "import", "bulk_edit", "assign"],
    ["crm.deals.detail"]     = ["view", "update", "delete", "assign", "share", "approve"],
    ["crm.deals.kanban"]     = ["view", "update"],
    ["crm.companies"]        = ["view", "create", "update", "delete", "export", "import", "merge"],
    ["crm.contacts"]         = ["view", "create", "update", "delete", "export", "import", "merge"],
    ["ai.chat"]              = ["view", "execute"],
    ["ai.scoring"]           = ["view", "execute", "configure"],
    ["library.documents"]    = ["view", "create", "update", "delete", "share", "archive"],
    ["reports.sales"]        = ["view", "export"],
    ["settings.members"]     = ["view", "create", "update", "delete", "assign"],
    ["system.roles"]         = ["view", "create", "update", "delete"],
    ["system.permissions"]   = ["view", "update"],
    // ... đầy đủ trong SalioDbSeeder.cs
};
```

→ Seeder generate `FunctionAction` rows từ đây → generate `Permission` rows (1 permission cho mỗi cặp valid, scope=Any).

Total permissions sinh ra: tổng số entries trong dict (~150+ permission).

## 6 System Roles

| Role | Code | Scope pattern |
|---|---|---|
| Super Admin | `super_admin` | `*` (mọi function × mọi action) |
| Owner | `owner` | `*` |
| Admin | `admin` | `crm.*`, `ai.*`, `library.*`, `reports.*`, `settings.*`, `system.users:view` |
| Manager | `manager` | `crm.deals.*`, `crm.companies:*`, `crm.contacts:*`, `reports.*` |
| Sales | `sales` | `crm.deals.list:view`, `crm.deals.list:create`, `crm.deals.list:update`, `crm.deals.detail:*`, `crm.companies:view/create/update`, `crm.contacts:*` |
| Viewer | `viewer` | `*:view` |

`IsSystem = true` → user không thể xóa, chỉ admin tạo custom role.

### Scope pattern matching (seeder)

Khi seeder bind pattern vào permission cụ thể:

```csharp
static bool Matches(string pattern, string fnCode, string actCode)
{
    // "*" → match tất cả
    if (pattern == "*") return true;

    var parts = pattern.Split(':');
    var fnPattern = parts[0];        // "crm.deals.*"
    var actPattern = parts.Length > 1 ? parts[1] : "*";  // "view"

    // function: glob (*) hoặc literal
    bool fnOk = fnPattern.EndsWith(".*")
        ? fnCode.StartsWith(fnPattern[..^1])  // bỏ "*", giữ "crm.deals."
        : fnPattern == fnCode;

    // action: "*" hoặc literal
    bool actOk = actPattern == "*" || actPattern == actCode;

    return fnOk && actOk;
}
```

Pattern hỗ trợ:

| Pattern | Match |
|---|---|
| `*` | Tất cả |
| `crm.*` | Function bắt đầu `crm.` |
| `crm.deals.*` | `crm.deals.list`, `crm.deals.detail`, `crm.deals.kanban`, `crm.deals.import` |
| `crm.deals.list` | Đúng function này, mọi action |
| `crm.deals.list:view` | Đúng function + đúng action |
| `*:view` | Mọi function, action = view |

## Resolve permission tại runtime

`PermissionChecker.HasPermissionAsync(userId, orgId, fnCode, actCode)`:

```
1. Tìm permIds = permissions WHERE function.code = fnCode AND action.code = actCode
   (1 permission/scope — hiện scope=Any → 1 row)

2. Check PermissionGrant DENY:
   EXISTS grant WHERE user_id=? AND org_id=? AND permission_id IN (permIds)
                   AND effect=Deny AND (expires_at IS NULL OR expires_at > now)
   → return false   (DENY ALWAYS WINS)

3. Check PermissionGrant ALLOW:
   Same WHERE + effect=Allow → return true

4. Check role-based:
   user_roles (chưa expire) join role_permissions
   WHERE user_id=? AND org_id=? AND permission_id IN (permIds)
   → return true

5. else → return false
```

### Code

```csharp
public async Task<bool> HasPermissionAsync(Guid userId, Guid orgId,
    string functionCode, string actionCode, CancellationToken ct = default)
{
    var permIds = await db.Permissions
        .Where(p => p.Function!.Code == functionCode && p.Action!.Code == actionCode)
        .Select(p => p.Id)
        .ToListAsync(ct);

    if (permIds.Count == 0) return false;

    var now = DateTimeOffset.UtcNow;

    // 1. Deny override
    var denied = await db.PermissionGrants.AnyAsync(g =>
        g.UserId == userId && g.OrgId == orgId &&
        permIds.Contains(g.PermissionId) &&
        g.Effect == GrantEffect.Deny &&
        (g.ExpiresAt == null || g.ExpiresAt > now), ct);
    if (denied) return false;

    // 2. Direct Allow
    var allowed = await db.PermissionGrants.AnyAsync(g =>
        g.UserId == userId && g.OrgId == orgId &&
        permIds.Contains(g.PermissionId) &&
        g.Effect == GrantEffect.Allow &&
        (g.ExpiresAt == null || g.ExpiresAt > now), ct);
    if (allowed) return true;

    // 3. Role-based
    return await db.UserRoles
        .Where(ur => ur.UserId == userId && ur.OrgId == orgId &&
                    (ur.ExpiresAt == null || ur.ExpiresAt > now))
        .Join(db.RolePermissions, ur => ur.RoleId, rp => rp.RoleId, (ur, rp) => rp)
        .AnyAsync(rp => permIds.Contains(rp.PermissionId), ct);
}
```

## `[RequirePermission]` attribute

Trên controller action:

```csharp
[HttpPost]
[RequirePermission("crm.deals.list", "create")]
public async Task<IActionResult> Create([FromBody] CreateDealCommand cmd, CancellationToken ct)
{
    var id = await Mediator.Send(cmd, ct);
    return CreatedAtAction(nameof(Get), new { id }, ApiResponse<Guid>.Ok(id));
}
```

Filter (`IAsyncAuthorizationFilter`):

```csharp
public async Task OnAuthorizationAsync(AuthorizationFilterContext ctx)
{
    var current = ctx.HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();
    var checker = ctx.HttpContext.RequestServices.GetRequiredService<IPermissionChecker>();

    if (!current.IsAuthenticated || current.UserId is null || current.OrgId is null)
    {
        ctx.Result = new UnauthorizedResult();
        return;
    }

    var ok = await checker.HasPermissionAsync(
        current.UserId.Value, current.OrgId.Value, _functionCode, _actionCode);

    if (!ok)
    {
        ctx.Result = new ObjectResult(new
        {
            success = false,
            error = new { code = "FORBIDDEN", message = $"Missing permission {_functionCode}:{_actionCode}" }
        }) { StatusCode = 403 };
    }
}
```

## Gán role cho user

API gợi ý (chưa implement):

```http
POST /api/v1/rbac/user-roles
{ "userId": "...", "roleId": "...", "expiresAt": null }
```

Logic:
1. Check current user có permission `system.roles:assign` (hoặc admin).
2. Check role tồn tại, thuộc cùng org (hoặc system role).
3. Tránh duplicate (`UserRole` đã có).
4. SaveChanges + audit log.

## Permission grant trực tiếp

Use case: cho 1 user lẻ thêm 1 quyền tạm thời (3 ngày).

```http
POST /api/v1/rbac/permission-grants
{
  "userId": "...",
  "permissionId": "...",
  "effect": "Allow",
  "expiresAt": "2026-05-16T00:00:00Z"
}
```

→ Override role-based; có `ExpiresAt` để auto-revoke.

Deny grant đặc biệt hữu ích: tạm "khóa" 1 quyền của user dù role cho phép.

## UI matrix (frontend)

Frontend lấy ma trận từ:
- `GET /api/v1/system/functions` → 30 function
- `GET /api/v1/system/actions` → 15 action
- `GET /api/v1/rbac/roles/{id}/permissions` (chưa implement) → permission của role

Render bảng `function × action` với checkbox; tick = role có permission. Update bằng `PUT /api/v1/rbac/roles/{id}/permissions` (đầy đủ list permission ids).

## Anti-pattern

- ❌ Hardcode role check (`if (user.Role == "admin")`) → dùng `HasPermissionAsync`.
- ❌ String-based permission free-form (`"can_edit_deals"`) → dùng function + action.
- ❌ Filter `OrgId` quên trong query → bao giờ cũng `.Where(x => x.OrgId == current.OrgId)`.
- ❌ Gán permission trực tiếp cho user nhiều → ưu tiên gán role + thêm grant chỉ khi exception.

## Performance

`HasPermissionAsync` chạy mỗi request có `[RequirePermission]` — 3 query DB. Tối ưu:

- **Cache** permission của user trong memory (Redis hoặc IMemoryCache), invalidate khi role/grant thay đổi.
- **Eager load** permissions vào JWT claims khi login (tăng size token nhưng giảm query). Trade-off: token stale khi grant thay đổi (max là access token lifetime = 30 phút).
