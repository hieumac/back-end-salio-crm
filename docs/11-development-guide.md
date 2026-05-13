# 11 — Development guide

> Hướng dẫn workflow phổ biến: thêm endpoint, thêm entity, thêm permission, debug, test.

## Setup môi trường

### Lần đầu

```bash
# 1. Clone repo
cd backend-dotnet

# 2. Restore packages
dotnet restore

# 3. Start DB
docker compose up -d

# 4. Build solution
dotnet build

# 5. Run
dotnet run --project src/Salio.Api
# Mở http://localhost:5080/swagger
```

### Hot reload khi dev

```bash
dotnet watch --project src/Salio.Api
```

### Reset DB hoàn toàn

```bash
docker compose down -v
docker compose up -d
dotnet run --project src/Salio.Api      # auto-migrate + auto-seed
```

## Thêm endpoint mới — quy trình 6 bước

Ví dụ: thêm `GET /api/v1/crm/companies` (list companies).

### Bước 1: Tạo DTO

`src/Salio.Application/Features/Companies/Dtos/CompanyDto.cs`:

```csharp
namespace Salio.Application.Features.Companies.Dtos;

public record CompanyListItemDto(
    Guid Id,
    string Name,
    string? Industry,
    string? Email,
    string? Phone,
    int DealCount,
    DateTimeOffset UpdatedAt);
```

### Bước 2: Tạo Query

`src/Salio.Application/Features/Companies/Queries/ListCompaniesQuery.cs`:

```csharp
public record ListCompaniesQuery(int Page = 1, int PageSize = 20, string? Q = null)
    : IRequest<PagedResult<CompanyListItemDto>>;

public class ListCompaniesQueryValidator : AbstractValidator<ListCompaniesQuery>
{
    public ListCompaniesQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public class ListCompaniesQueryHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<ListCompaniesQuery, PagedResult<CompanyListItemDto>>
{
    public async Task<PagedResult<CompanyListItemDto>> Handle(ListCompaniesQuery q, CancellationToken ct)
    {
        if (current.OrgId is null) throw new ForbiddenException("Org context missing");

        var query = db.Companies.Where(c => c.OrgId == current.OrgId.Value);

        if (!string.IsNullOrWhiteSpace(q.Q))
            query = query.Where(c => EF.Functions.ILike(c.Name, $"%{q.Q}%"));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((q.Page - 1) * q.PageSize).Take(q.PageSize)
            .Select(c => new CompanyListItemDto(
                c.Id, c.Name, c.Industry, c.Email, c.Phone,
                c.Deals.Count(d => d.DeletedAt == null),
                c.UpdatedAt))
            .ToListAsync(ct);

        return new PagedResult<CompanyListItemDto>
        {
            Items = items, Page = q.Page, PageSize = q.PageSize,
            Total = total, TotalPages = (int)Math.Ceiling((double)total / q.PageSize)
        };
    }
}
```

### Bước 3: Thêm function + permission vào seeder (nếu chưa có)

`SalioDbSeeder.cs` → `FunctionDefaultActions`:

```csharp
["crm.companies"] = ["view", "create", "update", "delete", "export", "import", "merge"],
```

Restart app → seeder tự tạo function + 7 permission.

### Bước 4: Thêm controller

`src/Salio.Api/Controllers/V1/CompaniesController.cs`:

```csharp
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/crm/companies")]
[Authorize]
public class CompaniesController : ApiControllerBase
{
    [HttpGet]
    [RequirePermission("crm.companies", "view")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CompanyListItemDto>>), 200)]
    public async Task<IActionResult> List([FromQuery] ListCompaniesQuery q, CancellationToken ct)
    {
        var result = await Mediator.Send(q, ct);
        return Ok(ApiResponse<PagedResult<CompanyListItemDto>>.Ok(result));
    }
}
```

### Bước 5: Gán quyền cho role (tùy chọn)

Nếu muốn role `sales` có quyền view → bổ sung pattern trong seeder:

```csharp
new SystemRoleSeed("sales", "Sales", new[] {
    "crm.deals.list:view",
    "crm.companies:view",         // ← thêm
    ...
})
```

### Bước 6: Test

```bash
curl -X GET "http://localhost:5080/api/v1/crm/companies?page=1&pageSize=10" \
  -H "Authorization: Bearer <jwt>"
```

Hoặc dùng Swagger UI: `http://localhost:5080/swagger`.

## Thêm entity mới

Ví dụ: thêm entity `Note` (ghi chú free-form).

### Bước 1: Tạo entity

`src/Salio.Domain/Entities/Crm/Note.cs`:

```csharp
namespace Salio.Domain.Entities.Crm;

public class Note : TenantEntity   // có Id, CreatedAt, UpdatedAt, DeletedAt, OrgId
{
    public Guid? DealId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }

    public Deal? Deal { get; set; }
    public User? Author { get; set; }
}
```

### Bước 2: Thêm DbSet vào `ISalioDbContext` + `SalioDbContext`

```csharp
// ISalioDbContext.cs
DbSet<Note> Notes { get; }

// SalioDbContext.cs
public DbSet<Note> Notes => Set<Note>();
```

### Bước 3: Configuration

`Persistence/Configurations/CrmConfigurations.cs`:

```csharp
public class NoteConfiguration : IEntityTypeConfiguration<Note>
{
    public void Configure(EntityTypeBuilder<Note> b)
    {
        b.ToTable("notes");
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Content).HasColumnType("text").IsRequired();
        b.HasIndex(x => new { x.OrgId, x.DealId });
        b.HasQueryFilter(x => x.DeletedAt == null);

        b.HasOne(x => x.Deal).WithMany().HasForeignKey(x => x.DealId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

### Bước 4: Tạo migration

```bash
dotnet ef migrations add AddNotes \
  --project src/Salio.Infrastructure \
  --startup-project src/Salio.Api
```

Review migration sinh ra. Apply:

```bash
dotnet ef database update --project src/Salio.Infrastructure --startup-project src/Salio.Api
```

Hoặc restart app (AutoMigrate=true).

### Bước 5: Thêm Command/Query, Controller, Permission

Theo flow ở mục "Thêm endpoint mới".

## Thêm field cho entity có sẵn

Ví dụ: thêm `LeadSource` cho `Deal`.

1. Thêm property vào `Deal.cs`:
   ```csharp
   public string? LeadSource { get; set; }
   ```
2. Update configuration (nếu cần custom column type/index):
   ```csharp
   b.Property(x => x.LeadSource).HasMaxLength(100);
   ```
3. Migration:
   ```bash
   dotnet ef migrations add AddLeadSourceToDeals --project src/Salio.Infrastructure --startup-project src/Salio.Api
   ```
4. Update DTO/Command/Validator/Handler tương ứng.

## Thêm permission tạm cho 1 user

Use case: cấp quyền `crm.deals.export:any` cho user X trong 7 ngày.

Direct DB (hoặc qua API khi đã implement):

```sql
INSERT INTO permission_grants (id, user_id, org_id, permission_id, effect, expires_at, created_at, updated_at)
SELECT
  gen_random_uuid(),
  '{user_id}',
  '{org_id}',
  p.id,
  'Allow',
  now() + interval '7 days',
  now(), now()
FROM permissions p
JOIN system_functions f ON p.function_id = f.id
JOIN system_actions a ON p.action_id = a.id
WHERE f.code = 'crm.deals.list' AND a.code = 'export';
```

## Debug

### Log SQL của EF Core

`appsettings.Development.json`:

```json
"Serilog": {
  "MinimumLevel": {
    "Override": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

Hoặc trong `DbContext`:

```csharp
opt.UseNpgsql(...)
   .LogTo(Console.WriteLine, LogLevel.Information)
   .EnableSensitiveDataLogging();   // chỉ ở dev — log parameter values
```

### Inspect database

```bash
docker exec -it salio-postgres psql -U salio -d salio

# Liệt kê bảng
\dt

# Schema bảng
\d deals

# Query
SELECT count(*) FROM permissions;
```

### Trace request

Mỗi response lỗi có `traceId` (W3C trace context). Trên Serilog log:

```
[INF] 17:23:45 (00-abc...) Handling CreateDealCommand
[ERR] 17:23:45 (00-abc...) ValidationException: Title is required
```

Grep theo traceId để theo dõi 1 request xuyên các middleware.

## Test (gợi ý setup)

### Unit test handler

```csharp
public class CreateDealCommandHandlerTests
{
    [Fact]
    public async Task Should_Generate_Code()
    {
        var opts = new DbContextOptionsBuilder<SalioDbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}").Options;
        var db = new SalioDbContext(opts);
        // seed pipeline + stage
        var current = new FakeCurrentUser(orgId: Guid.NewGuid(), userId: Guid.NewGuid());
        var handler = new CreateDealCommandHandler(db, current);

        var id = await handler.Handle(new CreateDealCommand("Test", 100, "USD", pipelineId, stageId, ...), default);

        var deal = await db.Deals.FindAsync(id);
        Assert.NotNull(deal);
        Assert.StartsWith($"DEAL-{DateTime.UtcNow:yyyyMM}-", deal.Code);
    }
}
```

### Integration test (Testcontainers)

```csharp
public class DealsApiTests : IAsyncLifetime
{
    private PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .Build();
    private WebApplicationFactory<Program> _app = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        _app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _pg.GetConnectionString()
                })));
    }

    [Fact]
    public async Task POST_Deals_Returns_201()
    {
        var client = _app.CreateClient();
        // login + get JWT
        // call POST /api/v1/crm/deals
        // assert 201
    }
}
```

## Code style

- **EditorConfig** + **Nullable enabled** — handle null cẩn thận.
- Tên file = tên class duy nhất (1 file 1 class, trừ DTO/handler/validator của 1 command để dồn 1 file).
- Async suffix: method async đặt tên `XxxAsync`.
- DI lifetime đúng (Singleton stateless, Scoped với DbContext).
- Không catch `Exception` chung; throw `DomainException` cụ thể.

## Performance

- `AsNoTracking()` cho query đọc thuần (list).
- Tránh N+1: dùng `Include` hoặc projection `Select`.
- Pagination bắt buộc với list endpoint.
- Index trên cột filter/sort thường dùng (`OrgId + ...`).
- Compiled query cho hot path.

## Git workflow

Commit message convention:

```
feat(crm): add list companies endpoint
fix(auth): refresh token rotation race condition
refactor(infra): split CrmConfigurations into 2 files
docs: update api endpoints
chore(deps): bump EF Core to 9.0.3
```

Branch:
- `main` — protected, chỉ merge qua PR
- `feature/<name>` — feature branch
- `fix/<name>` — bug fix
