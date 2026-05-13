# Salio Sales AI — Backend (.NET 9, Clean Architecture)

Backend C# .NET 9 cho Salio Sales AI, thiết kế theo **Clean Architecture** với 4 layer rõ ràng, REST API có versioning chuẩn, kết nối PostgreSQL 16 + pgvector.

## Kiến trúc

```
backend-dotnet/
├── Salio.sln
├── src/
│   ├── Salio.Domain          ← Entities, enums, value objects, domain exceptions
│   │   ├── Common/           (BaseEntity, AuditableEntity, TenantEntity, Result, Exceptions)
│   │   ├── Enums/
│   │   └── Entities/         (Identity, Auth, Rbac, Crm, Models, Duplicate, Ai, Library, Chat, Cross)
│   │
│   ├── Salio.Application     ← Use cases (CQRS), DTOs, interfaces (ports)
│   │   ├── Common/
│   │   │   ├── Interfaces/   (ISalioDbContext, ICurrentUserService, IJwtTokenService, IPermissionChecker)
│   │   │   └── Behaviors/    (ValidationBehavior, LoggingBehavior)
│   │   └── Features/         (Auth, Deals, ...)
│   │
│   ├── Salio.Infrastructure  ← EF Core, JWT, password hasher, seed
│   │   ├── Persistence/      (SalioDbContext, Configurations/, Migrations/, SalioDbSeeder)
│   │   ├── Services/         (BCryptPasswordHasher, JwtTokenService, PermissionChecker)
│   │   └── Configuration/    (JwtOptions)
│   │
│   └── Salio.Api             ← Controllers, middleware, Program.cs
│       ├── Controllers/V1/   (AuthController, DealsController, SystemFunctionsController, HealthController)
│       ├── Common/           (ApiResponse, Authorization/RequirePermissionAttribute)
│       ├── Middleware/       (ExceptionHandlingMiddleware)
│       ├── Services/         (CurrentUserService)
│       └── Program.cs
│
└── docker-compose.yml        ← PostgreSQL + pgvector
```

### Dependency direction (Clean Architecture)

```
Api  →  Application  →  Domain
 ↓           ↑
Infrastructure ┘
```

- **Domain** không phụ thuộc thư viện nào (POCO entities).
- **Application** chỉ phụ thuộc Domain, định nghĩa interface (ports). Dùng MediatR (CQRS) + FluentValidation.
- **Infrastructure** implement interface của Application — EF Core, Npgsql, JWT, BCrypt, pgvector.
- **Api** ráp tất cả lại, expose REST endpoints.

## Stack chính

| Layer | Library |
|---|---|
| ORM | EF Core 9 + Npgsql (PostgreSQL) + pgvector |
| CQRS | MediatR 12 |
| Validation | FluentValidation 11 |
| Mapping | Mapster |
| Auth | JWT Bearer + BCrypt |
| API Versioning | Asp.Versioning 8 |
| Logging | Serilog (Console + File) |
| API Docs | Swashbuckle (Swagger UI) |

## Quy ước endpoint

**Mẫu chuẩn:** `/api/v{version}/{module}/{resource}[/{id}][/{sub-resource}]`

Hỗ trợ 2 cách chỉ định version:
1. URL segment: `/api/v1/crm/deals`
2. Header: `X-Api-Version: 1.0`

### REST verb convention

| Method | Mục đích | Status code |
|---|---|---|
| `GET /resource` | List + pagination | 200 |
| `GET /resource/{id}` | Get by id | 200 / 404 |
| `POST /resource` | Create | 201 + Location |
| `PUT /resource/{id}` | Replace toàn bộ | 200 |
| `PATCH /resource/{id}` | Cập nhật một phần | 200 |
| `PATCH /resource/{id}/sub-action` | Sub-action (vd: `/deals/{id}/stage`) | 200 |
| `DELETE /resource/{id}` | Soft delete | 204 |

### Response envelope

Tất cả response dùng format chuẩn match với `ApiResponse<T>` ở frontend:

```json
{
  "success": true,
  "data": { ... },
  "message": null
}
```

Lỗi:

```json
{
  "success": false,
  "error": { "code": "FORBIDDEN", "message": "...", "details": [...] },
  "traceId": "00-..."
}
```

### Endpoints đã có (v1)

| Method | Path | Mô tả | Permission |
|---|---|---|---|
| `POST` | `/api/v1/auth/login` | Đăng nhập email/password | Anonymous |
| `POST` | `/api/v1/auth/register` | Đăng ký + tạo org | Anonymous |
| `POST` | `/api/v1/auth/refresh` | Refresh access token | Anonymous |
| `GET`  | `/api/v1/crm/deals` | List deals | `crm.deals.list:view` |
| `GET`  | `/api/v1/crm/deals/{id}` | Chi tiết deal | `crm.deals.detail:view` |
| `POST` | `/api/v1/crm/deals` | Tạo deal | `crm.deals.list:create` |
| `PATCH`| `/api/v1/crm/deals/{id}/stage` | Đổi stage | `crm.deals.detail:update` |
| `GET`  | `/api/v1/system/functions` | List functions cho RBAC matrix | `system.functions:view` |
| `GET`  | `/api/v1/system/actions` | List standard actions | `system.functions:view` |
| `GET`  | `/api/v1/health` | Health check | Anonymous |

## Setup nhanh

### 1. Khởi chạy PostgreSQL

```bash
cd backend-dotnet
docker compose up -d
```

Hoặc cài Postgres local — đảm bảo có extension `vector` và `uuid-ossp`:

```sql
CREATE DATABASE salio_dev;
\c salio_dev
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "vector";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
```

### 2. Cấu hình connection string

Chỉnh `src/Salio.Api/appsettings.Development.json` nếu DB không chạy mặc định:

```json
"ConnectionStrings": {
  "Default": "Host=localhost;Port=5432;Database=salio_dev;Username=salio;Password=salio123"
}
```

**Bảo mật:** đổi `Jwt:Secret` thành chuỗi random ≥ 64 ký tự. Trong production nên dùng `dotnet user-secrets` hoặc env var `Jwt__Secret`.

### 3. Tạo migration & chạy

```bash
# Cài EF tool (1 lần)
dotnet tool install --global dotnet-ef

# Restore packages
dotnet restore

# Tạo migration đầu tiên
dotnet ef migrations add Initial \
  --project src/Salio.Infrastructure \
  --startup-project src/Salio.Api \
  --output-dir Persistence/Migrations

# Chạy app — sẽ tự migrate + seed nếu Database:AutoMigrate=true / Database:AutoSeed=true
dotnet run --project src/Salio.Api
```

Mở Swagger UI: `http://localhost:5080/swagger`

### 4. Seed dữ liệu

Khi `Database:AutoSeed=true`, lúc startup app sẽ seed:
- 15 actions chuẩn (`view`, `create`, `update`, `delete`, `export`, ...)
- 30 system functions (theo các route trong frontend Salio)
- `function_actions` matrix (mỗi function có những action nào hợp lệ)
- `permissions` (function × action × scope)
- 6 system roles: `super_admin`, `owner`, `admin`, `manager`, `sales`, `viewer` với `role_permissions` tương ứng

Khi user đăng ký mới qua `/auth/register`, sẽ tự được gán role `owner` trong org vừa tạo.

## Quy ước code

- **Multi-tenant**: tất cả entity CRM thừa kế `TenantEntity` (có `OrgId`). Mọi truy vấn phải lọc theo `OrgId` của user hiện tại (lấy từ `ICurrentUserService.OrgId`).
- **Soft delete**: entity thừa kế `SoftDeletableEntity` không bị xóa cứng — `SalioDbContext.SaveChangesAsync` tự convert `Delete` thành set `DeletedAt`.
- **Audit timestamps**: `CreatedAt`/`UpdatedAt` tự cập nhật trong `SaveChangesAsync`.
- **Authorization**: dùng `[RequirePermission("function.code", "action")]` thay vì `[Authorize(Roles=...)]`. Permission được check qua `PermissionChecker` (đọc từ `permission_grants` + `user_roles` → `role_permissions`).
- **Naming**: snake_case trong DB (tên table/column theo `EntityTypeConfiguration`), PascalCase trong code. EF Core tự map.

## Thêm endpoint mới — quy trình

1. **Domain**: thêm entity nếu cần, viết business logic vào method của entity.
2. **Application**: tạo `Command/Query` + `Validator` + `Handler` trong `Features/{Module}/`.
3. **Infrastructure**: thêm `IEntityTypeConfiguration<T>` nếu có entity mới, chạy `dotnet ef migrations add ...`.
4. **Api**: thêm action vào controller, gắn `[RequirePermission(...)]`.
5. **Seed**: nếu có function mới, thêm vào `SalioDbSeeder.Functions` + `FunctionDefaultActions`.

## Cấu trúc test (chưa scaffold)

```
tests/
├── Salio.Domain.Tests
├── Salio.Application.Tests   (handler tests với in-memory DB)
└── Salio.Api.IntegrationTests (WebApplicationFactory + Testcontainers Postgres)
```
