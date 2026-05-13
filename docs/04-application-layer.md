# 04 — Application layer

> Application layer chứa **use cases (CQRS)**, **DTOs**, **validators**, và **interfaces (ports)** mà Infrastructure phải implement.

## CQRS qua MediatR

**Mỗi use case = 1 file** chứa Command/Query + Validator + Handler.

```csharp
public record CreateDealCommand(string Title, /*…*/) : IRequest<Guid>;

public class CreateDealCommandValidator : AbstractValidator<CreateDealCommand>
{
    public CreateDealCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
    }
}

public class CreateDealCommandHandler(ISalioDbContext db, ICurrentUserService current)
    : IRequestHandler<CreateDealCommand, Guid>
{
    public async Task<Guid> Handle(CreateDealCommand req, CancellationToken ct)
    {
        // … create deal
    }
}
```

Controller chỉ cần:

```csharp
var id = await Mediator.Send(cmd, ct);
```

MediatR sẽ:
1. Chạy `LoggingBehavior` (log start + elapsed)
2. Chạy `ValidationBehavior` (gọi tất cả `IValidator<TRequest>`)
3. Resolve handler, gọi `Handle`
4. Return response

## Command vs Query

| Loại | Ký hiệu | Mục đích | Side effect |
|---|---|---|---|
| Command | `XxxCommand` | Thay đổi state | Có (write DB) |
| Query | `XxxQuery` | Đọc state | Không |

Cùng dùng `IRequest<TResponse>` của MediatR — phân biệt qua naming.

## Pipeline behaviors

Đăng ký trong `DependencyInjection.AddApplication()`:

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(assembly);
    cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
```

### LoggingBehavior

Log mọi request name + thời gian xử lý:

```
[INFO] Handling CreateDealCommand
[INFO] Handled CreateDealCommand in 23ms
```

### ValidationBehavior

Tự động chạy mọi `IValidator<TRequest>` có trong DI:

```csharp
var failures = (await Task.WhenAll(validators.Select(v => v.ValidateAsync(...))))
    .SelectMany(r => r.Errors).ToList();
if (failures.Count != 0) throw new ValidationException(failures);
```

`ExceptionHandlingMiddleware` ở Api layer bắt → trả HTTP 422 với danh sách errors.

## Interfaces (ports)

Application **không biết** EF Core, JWT, BCrypt — chỉ biết các interface dưới đây. Infrastructure implement.

### `ISalioDbContext`

```csharp
public interface ISalioDbContext
{
    DbSet<Organization> Organizations { get; }
    DbSet<User> Users { get; }
    DbSet<Deal> Deals { get; }
    // … 46 DbSets

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
```

Handler nhận `ISalioDbContext` qua constructor injection. Không cần biết là `SalioDbContext` (EF Core).

### `ICurrentUserService`

```csharp
public interface ICurrentUserService
{
    Guid? UserId { get; }
    Guid? OrgId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Roles { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
}
```

Implement bởi `Salio.Api.Services.CurrentUserService` — đọc `HttpContext.User` claims.

### `IJwtTokenService`

```csharp
public interface IJwtTokenService
{
    (string AccessToken, string RefreshToken, DateTimeOffset AccessExpiresAt, DateTimeOffset RefreshExpiresAt)
        GenerateTokens(Guid userId, Guid orgId, string email, IEnumerable<string> roles);

    string HashRefreshToken(string rawToken);
}
```

### `IPasswordHasher`

```csharp
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}
```

### `IPermissionChecker`

```csharp
public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(Guid userId, Guid orgId, string functionCode, string actionCode, CancellationToken ct = default);
}
```

### `IDateTimeProvider`

```csharp
public interface IDateTimeProvider { DateTimeOffset UtcNow { get; } }
```

Dùng để test dễ — handler không gọi `DateTime.UtcNow` trực tiếp.

## DTOs

### Convention

- DTO là `record` (immutable, value semantics).
- Đặt trong `Features/{Module}/Dtos/`.
- Tách chi tiết và list-item: `DealDto` (full) vs `DealListItemDto` (gọn cho list).
- Map từ entity → DTO trong handler (Mapster hoặc thủ công với `Select`).

### Ví dụ

```csharp
public record DealDto(
    Guid Id,
    string Code,
    string Title,
    decimal Value,
    string Currency,
    /* … */ );

public record DealListItemDto(
    Guid Id,
    string Code,
    string Title,
    decimal Value,
    string Currency,
    Guid StageId,
    string? StageName,
    string? CompanyName,
    int? AiScore,
    DateOnly? ExpectedCloseDate);
```

## Use cases hiện có

### Auth

| Use case | Mục đích |
|---|---|
| `LoginCommand` | Login email/password, tạo session + refresh token, trả JWT |
| `RegisterCommand` | Tạo user + org + AuthIdentity + OrgMember, gán role owner |
| `RefreshTokenCommand` | Refresh token rotation, revoke token cũ, sinh token mới |

### Deals

| Use case | Mục đích |
|---|---|
| `ListDealsQuery` | Search/filter/pagination + sort |
| `GetDealByIdQuery` | Chi tiết deal |
| `CreateDealCommand` | Tạo deal mới, sinh code `DEAL-YYYYMM-NNNNN`, log activity |
| `MoveDealStageCommand` | Đổi stage, log `DealStageHistory`, set `ActualCloseDate` nếu IsWon/IsLost |

## Anti-pattern cần tránh

- ❌ Inject `SalioDbContext` (concrete) → dùng `ISalioDbContext`
- ❌ Trả entity ra controller → trả DTO
- ❌ Logic business ở handler → đẩy về entity method
- ❌ Throw `Exception` chung chung → throw `DomainException` cụ thể (`NotFoundException`, `ForbiddenException`...)
- ❌ Gọi `DateTime.UtcNow` trong handler → inject `IDateTimeProvider`

## Test handler (gợi ý setup)

```csharp
[Fact]
public async Task CreateDeal_Should_GenerateCode()
{
    var db = TestDbContext.Create();
    var current = new FakeCurrentUser(orgId: orgId, userId: userId);
    var handler = new CreateDealCommandHandler(db, current);

    var id = await handler.Handle(new CreateDealCommand("Test", /*…*/), default);

    var deal = db.Deals.Find(id);
    Assert.StartsWith("DEAL-", deal.Code);
}
```

Vì Application chỉ phụ thuộc interface, có thể dùng `Microsoft.EntityFrameworkCore.InMemory` hoặc Testcontainers Postgres cho test.
