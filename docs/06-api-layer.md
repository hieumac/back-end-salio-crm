# 06 — API layer

> Api layer là HTTP boundary — controllers, middleware, DI bootstrap, Swagger.

## `Program.cs` — bootstrap

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Logging
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/salio-.log", rollingInterval: RollingInterval.Day));

// 2. DI
builder.Services.AddApplication();              // MediatR + FluentValidation
builder.Services.AddInfrastructure(builder.Configuration);  // EF + JWT + …
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// 3. JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt => { /* validation params */ });
builder.Services.AddAuthorization();

// 4. API versioning
builder.Services.AddApiVersioning(o =>
{
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.AssumeDefaultVersionWhenUnspecified = true;
    o.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version"));
}).AddApiExplorer();

// 5. MVC + JSON
builder.Services.AddControllers().AddJsonOptions(o => /* camelCase, enum string */);

// 6. CORS
builder.Services.AddCors(opt => opt.AddPolicy("AllowFrontend", p => /* … */));

// 7. Swagger
builder.Services.AddSwaggerGen(c => /* JWT security definition */);

var app = builder.Build();

// 8. Migrate + seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SalioDbContext>();
    if (config.GetValue<bool>("Database:AutoMigrate")) await db.Database.MigrateAsync();
    if (config.GetValue<bool>("Database:AutoSeed")) await SalioDbSeeder.SeedAsync(db);
}

// 9. Pipeline
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(...); }
app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

## Controllers convention

### `ApiControllerBase`

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    protected ISender Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}
```

- Lazy resolve `ISender` để không cần constructor injection cho mọi controller.
- Route convention: `/api/v{version}/{controller}` — `DealsController` → `/api/v1/deals`. Override `[Route]` ở controller cụ thể để gắn module prefix (vd: `/api/v1/crm/deals`).

### Versioning trên từng controller

```csharp
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/crm/deals")]
[Authorize]
public class DealsController : ApiControllerBase { /* … */ }
```

Khi có v2:

```csharp
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/crm/deals")]
public class DealsControllerV2 : ApiControllerBase { /* … */ }
```

URL: `/api/v1/crm/deals` hoặc `/api/v2/crm/deals`. Hoặc gửi header `X-Api-Version: 2.0`.

### Action method pattern

```csharp
[HttpGet]
[RequirePermission("crm.deals.list", "view")]
[ProducesResponseType(typeof(ApiResponse<PagedResult<DealListItemDto>>), 200)]
public async Task<IActionResult> List([FromQuery] ListDealsQuery q, CancellationToken ct)
{
    var result = await Mediator.Send(q, ct);
    return Ok(ApiResponse<PagedResult<DealListItemDto>>.Ok(result));
}
```

Chỉ 3 dòng logic — bind, dispatch, wrap. Không có business logic.

## ApiResponse envelope

```csharp
public record ApiResponse<T>(bool Success, T? Data, string? Message = null)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, data, message);
}

public record ApiResponse(bool Success, string? Message = null)
{
    public static ApiResponse Ok(string? message = null) => new(true, message);
}
```

Response thành công:
```json
{ "success": true, "data": { ... }, "message": null }
```

Response lỗi (do `ExceptionHandlingMiddleware`):
```json
{
  "success": false,
  "error": { "code": "VALIDATION", "message": "...", "details": [...] },
  "traceId": "00-..."
}
```

Khớp với type `ApiResponse<T>` trong `frontend/src/types/common.ts`.

## ExceptionHandlingMiddleware

Bắt mọi exception, map sang HTTP status + JSON:

| Exception | Status | Code |
|---|---|---|
| `FluentValidation.ValidationException` | 422 | `VALIDATION` |
| `NotFoundException` | 404 | `NOT_FOUND` |
| `ForbiddenException` | 403 | `FORBIDDEN` |
| `ConflictException` | 409 | `CONFLICT` |
| `DomainException` (khác) | 400 | code của exception |
| `Exception` | 500 | `INTERNAL` |

## CurrentUserService (HttpContext → claims)

```csharp
public class CurrentUserService(IHttpContextAccessor accessor) : ICurrentUserService
{
    public Guid? UserId => Guid.TryParse(Principal?.FindFirst("sub")?.Value, out var id) ? id : null;
    public Guid? OrgId => Guid.TryParse(Principal?.FindFirst("org_id")?.Value, out var id) ? id : null;
    public string? Email => Principal?.FindFirst(ClaimTypes.Email)?.Value;
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
    public IReadOnlyList<string> Roles => Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    public string? UserAgent => accessor.HttpContext?.Request.Headers.UserAgent.ToString();
}
```

## `[RequirePermission]` attribute

```csharp
[RequirePermission("crm.deals.list", "create")]
public async Task<IActionResult> Create(...) { ... }
```

`IAsyncAuthorizationFilter` → chạy trước action:

1. Check `ICurrentUserService.IsAuthenticated` → nếu chưa login → 401
2. Gọi `IPermissionChecker.HasPermissionAsync(userId, orgId, fn, act)`
3. Nếu false → 403 với body `{ success: false, error: { code: "FORBIDDEN", message: "Missing permission ..." } }`

## Swagger UI

Bật ở Development: `http://localhost:5080/swagger`

- Liệt kê mọi endpoint theo version (group v1, v2…).
- Hỗ trợ JWT bearer — click "Authorize", paste `Bearer <token>` để gọi endpoint có `[Authorize]`.
- Đọc XML doc comment để hiển thị summary.

Production thường tắt Swagger hoặc đưa sau gateway có auth.

## CORS

```json
"Cors": {
  "Origins": [ "http://localhost:5173", "http://localhost:5174", "https://salio.local" ]
}
```

Policy `AllowFrontend` allow credentials → cho phép gửi `Authorization` header + cookie. Frontend Vue ở `:5173` (vite dev).

## Pipeline order quan trọng

```
Request
  ↓
Serilog request logging
  ↓
ExceptionHandlingMiddleware    ← phải sớm để bắt mọi exception bên trong
  ↓
[HTTPS redirect]
  ↓
CORS
  ↓
Authentication (JWT bearer)    ← parse token → HttpContext.User
  ↓
Authorization (policy + [RequirePermission])
  ↓
Endpoint routing
  ↓
Controller action
  ↓
MediatR pipeline (Logging → Validation → Handler)
```

## launchSettings.json

```json
{
  "profiles": {
    "http":  { "applicationUrl": "http://localhost:5080",  "launchUrl": "swagger" },
    "https": { "applicationUrl": "https://localhost:7080", "launchUrl": "swagger" }
  }
}
```
