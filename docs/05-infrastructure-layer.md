# 05 — Infrastructure layer

> Infrastructure implement các interface ports của Application — EF Core (PostgreSQL + pgvector), JWT, BCrypt, permission checker, seeder.

## `SalioDbContext`

Implement `ISalioDbContext`, kế thừa `DbContext`.

### Đăng ký DbSets

```csharp
public DbSet<Organization> Organizations => Set<Organization>();
public DbSet<User> Users => Set<User>();
public DbSet<Deal> Deals => Set<Deal>();
// … 46 DbSets
```

### Postgres extensions

```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    builder.HasPostgresExtension("uuid-ossp");
    builder.HasPostgresExtension("vector");
    builder.HasPostgresExtension("pg_trgm");

    builder.ApplyConfigurationsFromAssembly(typeof(SalioDbContext).Assembly);
}
```

Migration sẽ chạy `CREATE EXTENSION` tự động.

### `SaveChangesAsync` — audit + soft delete

```csharp
public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
{
    var now = DateTimeOffset.UtcNow;

    // Audit timestamps
    foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
    {
        if (entry.State == EntityState.Added) entry.Entity.CreatedAt = now;
        if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            entry.Entity.UpdatedAt = now;
    }

    // Soft delete
    foreach (var entry in ChangeTracker.Entries<SoftDeletableEntity>())
    {
        if (entry.State == EntityState.Deleted)
        {
            entry.State = EntityState.Modified;
            entry.Entity.DeletedAt = now;
        }
    }

    return await base.SaveChangesAsync(ct);
}
```

## Entity configurations

Chia theo module để file không quá dài:

| File | Configures |
|---|---|
| `IdentityConfigurations.cs` | Organization, User, OrgMember |
| `AuthConfigurations.cs` | AuthIdentity, UserSession, RefreshToken, EmailVerificationToken, PasswordResetToken, MfaFactor, MfaChallenge, LoginAttempt, ApiKey, Invitation |
| `RbacConfigurations.cs` | SystemFunction, SystemAction, FunctionAction, Permission, Role, RolePermission, UserRole, PermissionGrant, Team, TeamMember |
| `CrmConfigurations.cs` | Company, Contact, Pipeline, PipelineStage, Deal, DealActivity, DealStageHistory, Product, DealProduct, Task, DealFollower |
| `RemainingConfigurations.cs` | DuplicateMatchGroup, DuplicateMatchRecord, AiInsight, AiScoreHistory, LibraryNode, LibraryPermission, DocumentChunk, ChatConversation, ChatMessage, ChatMessageSource, Notification, AuditLog |

### Pattern chuẩn

```csharp
public class DealConfiguration : IEntityTypeConfiguration<Deal>
{
    public void Configure(EntityTypeBuilder<Deal> b)
    {
        b.ToTable("deals");                                       // snake_case table

        // Columns
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.Property(x => x.Value).HasPrecision(18, 2);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);  // enum → varchar
        b.Property(x => x.CustomFields).HasColumnType("jsonb");

        // Indexes
        b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
        b.HasIndex(x => new { x.OrgId, x.StageId });

        // Soft delete filter — query mặc định bỏ qua deleted
        b.HasQueryFilter(x => x.DeletedAt == null);

        // Relations
        b.HasOne(x => x.Pipeline).WithMany(p => p.Deals)
            .HasForeignKey(x => x.PipelineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Stage).WithMany(s => s.Deals)
            .HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Assignee).WithMany(u => u.AssignedDeals)
            .HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.SetNull);
    }
}
```

### Quy ước OnDelete

| Quan hệ | OnDelete |
|---|---|
| Owner mạnh (Organization → entities) | `Cascade` |
| Master data (Pipeline → Deal, Product → DealProduct) | `Restrict` |
| Optional FK (Company → Deal, Assignee → Deal) | `SetNull` |
| Composite/junction (RolePermission, DealFollower) | `Cascade` |

### pgvector

```csharp
b.Property(x => x.Embedding).HasColumnType("vector(1536)");
```

Dimension 1536 cho OpenAI `text-embedding-ada-002`. Đổi sang 768 nếu dùng model nhỏ hơn.

Đăng ký Npgsql:

```csharp
opt.UseNpgsql(connStr, npg => npg.UseVector());
```

## Services

### `BCryptPasswordHasher`

```csharp
public string Hash(string password) =>
    BCrypt.Net.BCrypt.EnhancedHashPassword(password, workFactor: 12);

public bool Verify(string password, string hash) =>
    BCrypt.Net.BCrypt.EnhancedVerify(password, hash);
```

`workFactor: 12` ≈ ~250ms/hash trên CPU hiện đại. Có thể tăng/giảm trong production.

### `JwtTokenService`

- Access token: JWT HS256 ký bằng `Jwt:Secret`, claims `sub`, `email`, `jti`, `org_id`, `role` (multi).
- Refresh token: random 256-bit base64-url, **lưu hash SHA-256** trong DB (raw chỉ tồn tại trong response).
- TTL: access 30 phút (cấu hình), refresh 30 ngày.

### `PermissionChecker`

Thuật toán resolve permission (xem `10-authorization-rbac.md`):

1. Tìm các permission ID khớp `function.code + action.code`.
2. Nếu có `PermissionGrant` với effect=Deny chưa expire → **false** (deny override).
3. Nếu có `PermissionGrant` với effect=Allow chưa expire → **true**.
4. Nếu user có `UserRole` (chưa expire) với role có `RolePermission` → **true**.
5. Else → **false**.

```csharp
public async Task<bool> HasPermissionAsync(Guid userId, Guid orgId, string fn, string act, CancellationToken ct)
{
    var permIds = await db.Permissions
        .Where(p => p.Function!.Code == fn && p.Action!.Code == act)
        .Select(p => p.Id).ToListAsync(ct);
    // … logic ở trên
}
```

## Seeder

`SalioDbSeeder.SeedAsync(db, ct)` chạy lúc app khởi động nếu `Database:AutoSeed=true`. **Idempotent** — gọi nhiều lần không tạo trùng.

### Trình tự seed

1. **Actions**: 15 actions chuẩn (view, create, update, delete, export, import, approve, assign, merge, transfer, share, execute, configure, archive, bulk_edit)
2. **Functions**: 30 system functions (dashboard.*, crm.*, ai.*, library.*, reports.*, settings.*, system.*)
3. **FunctionActions**: theo bảng `FunctionDefaultActions` — định nghĩa function nào support action nào
4. **Permissions**: Cartesian từ FunctionAction × scope=any (auto-gen)
5. **System roles**: 6 roles (super_admin, owner, admin, manager, sales, viewer) với scope patterns (`*`, `crm.*`, `crm.deals.*:view`, …) → resolve về RolePermission

### Mapping scope patterns

| Pattern | Match |
|---|---|
| `*` | tất cả |
| `crm.*` | mọi function bắt đầu `crm.` |
| `crm.deals.*` | mọi function `crm.deals.xxx` |
| `crm.deals.list` | đúng function đó với mọi action |
| `crm.deals.list:view` | đúng function + đúng action |

## DI registration

```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
{
    services.AddDbContext<SalioDbContext>(opt =>
        opt.UseNpgsql(config.GetConnectionString("Default"),
            npg => { npg.UseVector(); npg.MigrationsHistoryTable("__ef_migrations", "salio"); }));

    services.AddScoped<ISalioDbContext>(sp => sp.GetRequiredService<SalioDbContext>());

    services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
    services.AddSingleton<IJwtTokenService, JwtTokenService>();
    services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
    services.AddScoped<IPermissionChecker, PermissionChecker>();
    services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

    return services;
}
```

Lifetime:
- `DbContext`: **Scoped** (per HTTP request)
- `ISalioDbContext`: **Scoped** (forward về SalioDbContext)
- `JwtTokenService`, `BCryptPasswordHasher`: **Singleton** (stateless)
- `PermissionChecker`: **Scoped** (cần DbContext)

## Migration files

Sinh ra bằng EF CLI, lưu tại `src/Salio.Infrastructure/Persistence/Migrations/`. Schema migrations history: `salio.__ef_migrations`.

## Tương lai (gợi ý)

- **EF Interceptor cho Audit log** — auto-insert `AuditLog` mỗi command thay đổi entity.
- **Row Level Security** — tạo migration SQL thuần để bật RLS theo `org_id`.
- **DbContext pooling** — `AddDbContextPool<SalioDbContext>` cho high traffic.
- **Outbox pattern** — bảng `outbox_messages` cho integration events.
