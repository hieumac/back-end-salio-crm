# 01 — Clean Architecture

> Backend tổ chức theo **Clean Architecture (Onion / Hexagonal)** của Uncle Bob, áp dụng các nguyên tắc DDD-lite.

## Dependency Rule

Quy tắc duy nhất phải nhớ: **luồng phụ thuộc luôn hướng vào trong, không bao giờ ngược lại.**

```
        ┌──────────────────────────────────────────┐
        │                                          │
        │            Salio.Api                     │  ← I/O, framework, controllers, DI
        │  (Controllers, Middleware, Program.cs)   │
        │                                          │
        │   ┌──────────────────────────────────┐   │
        │   │     Salio.Infrastructure         │   │  ← EF Core, JWT, BCrypt, external services
        │   │  (DbContext, Configurations,     │   │
        │   │   JwtTokenService, Seeder, …)    │   │
        │   │                                  │   │
        │   │   ┌──────────────────────────┐   │   │
        │   │   │   Salio.Application      │   │   │  ← Use cases (CQRS), DTOs, interfaces
        │   │   │  (Commands, Queries,     │   │   │
        │   │   │   Validators, ports)     │   │   │
        │   │   │                          │   │   │
        │   │   │   ┌──────────────────┐   │   │   │
        │   │   │   │  Salio.Domain    │   │   │   │  ← Entities, enums, value objects
        │   │   │   │  (POCO entities, │   │   │   │     KHÔNG phụ thuộc gì
        │   │   │   │  Result, Excs)   │   │   │   │
        │   │   │   └──────────────────┘   │   │   │
        │   │   └──────────────────────────┘   │   │
        │   └──────────────────────────────────┘   │
        └──────────────────────────────────────────┘
```

| Project | Phụ thuộc | Nội dung |
|---|---|---|
| **Salio.Domain** | _Không gì cả_ (chỉ BCL + Pgvector type) | Business entities, enums, exceptions, value objects |
| **Salio.Application** | Domain | Use cases (CQRS), DTOs, validators, **interfaces** (ports) cho hạ tầng |
| **Salio.Infrastructure** | Application, Domain | Implement các interfaces của Application — EF Core, JWT, hash, mail, queue… |
| **Salio.Api** | Application, Infrastructure | Wiring DI, expose REST controllers, middleware |

## Vì sao dùng Clean Architecture

- **Test dễ**: viết unit test cho Domain & Application không cần DB/HTTP.
- **Đổi hạ tầng dễ**: muốn đổi từ PostgreSQL sang SQL Server, đổi MediatR sang library khác — chỉ chỉnh Infrastructure, không động vào logic nghiệp vụ.
- **Frontend agnostic**: cùng Application có thể serve REST hoặc gRPC hoặc GraphQL bằng cách thêm project Api khác.
- **Tách bạch trách nhiệm**: developer dễ tìm code đúng chỗ.

## Quy tắc thực hành

### Trong Domain

- Chỉ POCO entities — không annotation EF, không attribute MVC.
- Logic business nên ở method của entity, không phải ở handler.
- Throw `DomainException` (hoặc subclass) khi business rule vi phạm.
- Không gọi DB, không gọi HTTP, không gọi `DateTime.Now`.

```csharp
// Tốt
public class Deal {
    public void MoveTo(PipelineStage newStage, Guid actor) {
        if (Stage.IsLost) throw new DomainException("Cannot move lost deal");
        // ...
    }
}

// Không tốt - logic ở handler
public class MoveDealHandler {
    public async Task Handle(...) {
        if (deal.Stage.IsLost) ...  // ← business rule nên ở entity
    }
}
```

### Trong Application

- Mỗi use case = 1 file Command/Query + Handler + Validator.
- Phụ thuộc qua **interface** (`ISalioDbContext`, `IPasswordHasher`, …) — không phụ thuộc trực tiếp EF Core hay BCrypt.
- Không return entity domain ra ngoài → dùng DTO.

### Trong Infrastructure

- Implement các interface trong `Salio.Application.Common.Interfaces`.
- EF Core configurations ở đây (không phải Domain).
- External services: gửi email, gọi OpenAI, push notification, queue…

### Trong Api

- Controllers chỉ làm 3 việc: bind input → `Mediator.Send(...)` → return response chuẩn.
- Không có logic nghiệp vụ trong controller.
- Authorization qua `[RequirePermission(...)]` attribute.

## Cross-cutting concerns

Được giải quyết qua **MediatR Pipeline Behaviors** (chạy trước/sau handler):

| Behavior | Chức năng |
|---|---|
| `LoggingBehavior` | Log mọi request + elapsed time |
| `ValidationBehavior` | Chạy `IValidator<TRequest>` trước khi tới handler |
| _(Future)_ `TransactionBehavior` | Bọc tất cả write trong transaction |
| _(Future)_ `CachingBehavior` | Cache query response |
| _(Future)_ `AuditBehavior` | Auto-write audit log cho mọi command |

## Anti-pattern cần tránh

- ❌ Inject `SalioDbContext` trực tiếp vào controller → dùng `ISender` (MediatR) hoặc service tầng Application.
- ❌ Đặt logic phê duyệt deal trong controller → đặt vào entity hoặc handler.
- ❌ Trả entity domain ra HTTP → trả DTO.
- ❌ `using Microsoft.EntityFrameworkCore;` trong Domain → vi phạm Dependency Rule.
- ❌ Khởi tạo `new DateTime.UtcNow` trong Domain → inject `IDateTimeProvider`.
