# Salio Sales AI — Backend (.NET 9 + PostgreSQL + pgvector)

Backend cho **Salio Sales AI** — CRM thông minh có AI hỗ trợ phân tích deal, scoring, gợi ý next-best-action.
Viết bằng **C# .NET 9** theo **Clean Architecture** 4 lớp, expose REST API có versioning, sử dụng **PostgreSQL 16 + pgvector** (cho embedding/AI search).

---

## Mục lục

1. [Tổng quan kiến trúc](#1-tổng-quan-kiến-trúc)
2. [Stack chính](#2-stack-chính)
3. [Yêu cầu môi trường (Prerequisites)](#3-yêu-cầu-môi-trường-prerequisites)
4. [Cài đặt step-by-step](#4-cài-đặt-step-by-step)
5. [Build & Run](#5-build--run)
6. [Tài khoản & API mẫu](#6-tài-khoản--api-mẫu)
7. [Response template chuẩn](#7-response-template-chuẩn)
8. [Cấu hình & Secrets](#8-cấu-hình--secrets)
9. [Quy trình thêm endpoint mới](#9-quy-trình-thêm-endpoint-mới)
10. [Troubleshooting](#10-troubleshooting)
11. [Tài liệu bổ sung](#11-tài-liệu-bổ-sung)

---

## 1. Tổng quan kiến trúc

```
back-end-salio-crm/
├── Salio.sln
├── db/migrations/                ← SQL migration thủ công (trigger, comment, partial index)
├── docs/                         ← Tài liệu (API reference, DB schema)
├── docker-compose.yml            ← PostgreSQL + pgvector cho dev
├── src/
│   ├── Salio.Domain              ← Entity, enum, exception, base entity (POCO, không phụ thuộc lib nào)
│   ├── Salio.Application         ← Use-case CQRS (Command/Query + Handler), DTO, port interface
│   ├── Salio.Infrastructure      ← EF Core, JWT, password hasher, seeder
│   └── Salio.Api                 ← Controller, middleware, Swagger, Program.cs
└── tests/                        ← (placeholder) unit + integration
```

**Quy tắc phụ thuộc Clean Architecture:**

```
Api  →  Application  →  Domain
 ↓           ↑
Infrastructure ┘
```

- `Domain` không phụ thuộc gì ngoài BCL — POCO entities, không có annotation EF.
- `Application` chỉ phụ thuộc `Domain`, định nghĩa **interface** (`ISalioDbContext`, `ICurrentUserService`, `IJwtTokenService`, `IPermissionChecker`).
- `Infrastructure` implement các interface đó — EF Core, Npgsql, JWT, BCrypt, pgvector.
- `Api` chỉ ráp lại + expose REST endpoints.

---

## 2. Stack chính

| Layer | Library | Version |
|---|---|---|
| Runtime | .NET | 9.0 |
| Web framework | ASP.NET Core | 9.0 |
| ORM | EF Core + Npgsql + Pgvector | 9.0.4 / 0.3.2 |
| CQRS | MediatR | 12.4 |
| Validation | FluentValidation | 11.10 |
| Mapping | Mapster | 7.4 |
| Auth | JWT Bearer + BCrypt | – |
| API versioning | Asp.Versioning | 8.1 |
| Logging | Serilog (Console + File) | 8.0 |
| API docs | Swashbuckle (Swagger UI) | 7.2 |
| DB | PostgreSQL + pgvector + pg_trgm | 16+ / 0.7+ |

---

## 3. Yêu cầu môi trường (Prerequisites)

Cần cài 3 thứ trên máy phát triển:

| Tool | Version | Kiểm tra | Link |
|---|---|---|---|
| **.NET SDK** | 9.0 hoặc mới hơn | `dotnet --version` | <https://dotnet.microsoft.com/download/dotnet/9.0> |
| **PostgreSQL** | 15 hoặc mới hơn | `psql --version` | <https://www.postgresql.org/download/> hoặc dùng Docker |
| **pgvector** | 0.7+ | (cài cùng PG) | <https://github.com/pgvector/pgvector> — hoặc dùng image `pgvector/pgvector:pg16` |
| Git | bất kỳ | `git --version` | – |
| Docker (khuyến nghị) | bất kỳ | `docker --version` | – |

**Tùy chọn (làm việc với DB):**

- DBeaver / pgAdmin / TablePlus / DataGrip — để xem dữ liệu, chạy SQL.
- Postman / Bruno / Insomnia — để test API ngoài Swagger.

---

## 4. Cài đặt step-by-step

### Bước 1 — Clone source và mở solution

```bash
git clone <repo-url> back-end-salio-crm
cd back-end-salio-crm
```

Mở `Salio.sln` bằng Visual Studio 2022 / Rider / VS Code (kèm extension C# Dev Kit).

### Bước 2 — Cài .NET 9 SDK

Tải bản phù hợp OS từ <https://dotnet.microsoft.com/download/dotnet/9.0> → cài → mở terminal mới → verify:

```bash
dotnet --version
# Phải in ra 9.x.x
```

### Bước 3 — Chuẩn bị PostgreSQL

**Cách A — Docker (đơn giản nhất, khuyến nghị cho dev):**

```bash
docker compose up -d postgres
# Container 'salio-postgres' chạy ở localhost:5432
# user/pass/db: salio / salio123 / salio_dev (theo docker-compose.yml)
```

Verify container chạy:

```bash
docker ps | grep salio-postgres
docker logs salio-postgres --tail 20    # Đợi thấy "database system is ready"
```

**Cách B — Cài PostgreSQL native + pgvector:**

1. Cài PostgreSQL 16 từ <https://www.postgresql.org/download/>.
2. Cài pgvector — Windows: tải binary từ <https://github.com/pgvector/pgvector#windows-prebuilt-binaries>; Linux/Mac: `apt install postgresql-16-pgvector` hoặc build từ source.
3. Tạo database + role:

```bash
psql -U postgres
```

```sql
CREATE ROLE salio WITH LOGIN PASSWORD 'salio123';
CREATE DATABASE salio_dev OWNER salio;
\c salio_dev
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "vector";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";
\q
```

### Bước 4 — Cấu hình connection string

Mặc định `src/Salio.Api/appsettings.json` đã trỏ tới DB local:

```json
"ConnectionStrings": {
  "Default": "Host=localhost;Port=5432;Database=salio;Username=salio;Password=salio123;Include Error Detail=true"
}
```

Nếu DB của bạn khác (port khác, password khác, hoặc tên DB `salio_dev` thay vì `salio`), tạo file `appsettings.Development.json` cùng thư mục để override:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=salio_dev;Username=salio;Password=salio123"
  }
}
```

### Bước 5 — Đặt JWT Secret (BẮT BUỘC trong production)

`appsettings.json` đang để placeholder. Trên dev có thể giữ nguyên, **nhưng production cần override** bằng 1 trong 3 cách:

```bash
# Cách 1 — User Secrets (chỉ trên máy dev)
cd src/Salio.Api
dotnet user-secrets set "Jwt:Secret" "$(openssl rand -base64 64)"

# Cách 2 — Environment variable
export Jwt__Secret="$(openssl rand -base64 64)"

# Cách 3 — appsettings.Production.json
# {"Jwt": {"Secret": "your-64+-char-secret-here"}}
```

### Bước 6 — Restore packages

```bash
dotnet restore Salio.sln
```

Restore mất 30–60s lần đầu (tải NuGet packages).

### Bước 7 — Tạo schema database

**Khuyến nghị — dùng file SQL init một lần (nhanh, không cần EF CLI):**

```bash
psql -h localhost -U salio -d salio_dev \
     -f db/migrations/2026_05_18_000_init_schema.sql
```

File này tạo **toàn bộ 46 bảng + index + trigger `updated_at` + 3 extension** (uuid-ossp, vector, pg_trgm) trong 1 transaction. Idempotent — chạy nhiều lần an toàn nhờ `IF NOT EXISTS`.

Verify schema đúng:

```bash
# Tổng số bảng (kỳ vọng: 43)
psql -h localhost -U salio -d salio_dev -c \
     "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = current_schema();"

psql -h localhost -U salio -d salio_dev -c "\dt"          # list tables
psql -h localhost -U salio -d salio_dev -c "\d users"     # xem column users
```

**Cách thay thế — dùng EF Core migrations (cho ai quen workflow code-first):**

```bash
# Cài CLI tool 1 lần
dotnet tool install --global dotnet-ef --version 9.*

# Scaffold migration nếu thư mục Migrations trống
dotnet ef migrations add InitialCreate \
  --project src/Salio.Infrastructure \
  --startup-project src/Salio.Api \
  --output-dir Persistence/Migrations

# Áp migration EF Core
dotnet ef database update \
  --project src/Salio.Infrastructure \
  --startup-project src/Salio.Api

# Áp migration SQL bổ sung (thêm 10 base fields cho mỗi bảng)
psql -h localhost -U salio -d salio_dev \
     -f db/migrations/2026_05_18_001_add_base_fields.sql
```

> **Lưu ý:** Chỉ chọn 1 trong 2 cách trên — đừng chạy cả init schema lẫn EF migrations cùng lúc trên cùng database (sẽ conflict). Init schema là cách nhanh cho dev mới setup, EF migrations là cách evolve schema khi đã có dữ liệu production.

### Bước 8 — Seed dữ liệu RBAC (tự động khi start app)

Khi `appsettings.json` có `"Database": { "AutoMigrate": true, "AutoSeed": true }`, lần đầu app khởi động sẽ:

- Seed 15 actions chuẩn (`view`, `create`, `update`, `delete`, `export`, ...).
- Seed 30 system functions (theo route ở frontend Salio).
- Seed `function_actions` matrix (mỗi function có action hợp lệ nào).
- Seed `permissions` (function × action × scope).
- Seed 6 system roles: `super_admin`, `owner`, `admin`, `manager`, `sales`, `viewer` + `role_permissions`.

Idempotent — chạy nhiều lần không tạo trùng.

---

## 5. Build & Run

### Build tất cả

```bash
dotnet build Salio.sln
```

Output: `bin/Debug/net9.0/Salio.Api.dll` (và các project con).

### Run API (development)

```bash
dotnet run --project src/Salio.Api
```

Default URL:

- HTTP:  `http://localhost:5080`
- HTTPS: `https://localhost:7080`
- **Swagger UI**: <http://localhost:5080/swagger>
- **Health check**: <http://localhost:5080/api/v1/health>

Để chạy với hot-reload:

```bash
dotnet watch run --project src/Salio.Api
```

### Run từ Visual Studio / Rider

Mở `Salio.sln` → set startup project = `Salio.Api` → F5 (Visual Studio) hoặc nút Run (Rider).

### Run trong production-mode (test build release)

```bash
dotnet publish src/Salio.Api -c Release -o ./publish
cd publish
ASPNETCORE_ENVIRONMENT=Production \
ConnectionStrings__Default="Host=...;..." \
Jwt__Secret="your-secret-here" \
dotnet Salio.Api.dll
```

---

## 6. Tài khoản & API mẫu

### Đăng ký user đầu tiên (sẽ tự thành owner của org)

```bash
curl -X POST http://localhost:5080/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{
        "email": "admin@salio.local",
        "password": "Salio@2026",
        "fullName": "Admin User",
        "orgName": "Salio Demo Org",
        "orgSlug": "demo"
      }'
```

Response:

```json
{ "status": "success", "code": 201, "data": "9b9c1a4e-...", "message": "User registered" }
```

### Login

```bash
curl -X POST http://localhost:5080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@salio.local","password":"Salio@2026","orgSlug":"demo"}'
```

Response chứa `accessToken` + `refreshToken`:

```json
{
  "status": "success",
  "code": 200,
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "...",
    "accessExpiresAt": "2026-05-18T16:00:00Z",
    "refreshExpiresAt": "2026-06-17T15:30:00Z"
  }
}
```

### Gọi endpoint cần auth

```bash
TOKEN="eyJhbGc..."
curl http://localhost:5080/api/v1/users/me \
  -H "Authorization: Bearer $TOKEN"
```

Mở Swagger → click nút **Authorize** ở góc phải → dán `Bearer <token>` → thử mọi endpoint.

---

## 7. Response template chuẩn

Mọi response (kể cả lỗi) đều bọc cùng 1 cấu trúc:

```json
{
  "status": "success",       // hoặc "error"
  "code": 200,               // HTTP status code
  "message": "Lấy thông tin người dùng thành công",
  "data": { "id": 12345, "username": "hoangnv" },
  "errors": null,            // chỉ có khi lỗi
  "traceId": null            // chỉ có khi lỗi
}
```

Xem chi tiết format thành công / lỗi / validation: [`docs/api-reference.md`](docs/api-reference.md) → mục "Response wrapper".

---

## 8. Cấu hình & Secrets

`src/Salio.Api/appsettings.json` chứa các section sau:

| Section | Mô tả |
|---|---|
| `ConnectionStrings:Default` | Chuỗi kết nối PostgreSQL. |
| `Jwt` | `Secret` (≥64 ký tự), `Issuer`, `Audience`, `AccessTokenLifetimeMinutes`, `RefreshTokenLifetimeDays`. |
| `Database` | `AutoMigrate` (chạy `Database.Migrate()` khi start), `AutoSeed` (chạy `SalioDbSeeder`). |
| `Cors:Origins` | Mảng origin được phép gọi API (frontend dev/prod URL). |
| `Swagger:Enabled` | Bật Swagger UI ngoài môi trường Development. |
| `Serilog` | Cấu hình log level + sink. |

**Override theo môi trường:** dùng `appsettings.{Environment}.json`, user-secrets, hoặc env vars dạng `Section__Key=value`.

---

## 9. Quy trình thêm endpoint mới

1. **Domain** (`src/Salio.Domain/Entities/...`) — thêm entity (POCO), gắn đúng `BaseEntity` / `AuditableEntity` / `SoftDeletableEntity` / `TenantEntity`. Business logic là method của entity.
2. **Application** (`src/Salio.Application/Features/{Module}/`) — thêm `Command`/`Query` (implement `IRequest<TResponse>`) + `Validator` (FluentValidation) + `Handler`.
3. **Infrastructure** (`src/Salio.Infrastructure/Persistence/Configurations/`) — thêm `IEntityTypeConfiguration<T>` nếu có entity mới → chạy `dotnet ef migrations add Add{Feature}`.
4. **Api** (`src/Salio.Api/Controllers/V1/`) — thêm action vào controller, gắn `[RequirePermission("function.code", "action")]`.
5. **Seed** — nếu có function mới, thêm vào `SalioDbSeeder.Functions` + `FunctionDefaultActions`.

---

## 10. Troubleshooting

### `Could not connect to database` / `Connection refused`

→ Postgres chưa chạy hoặc connection string sai. Kiểm tra `docker ps`, thử `psql -h localhost -U salio -d salio_dev` để verify kết nối.

### `Extension "vector" is not available`

→ Postgres bạn cài không có pgvector. Dùng image `pgvector/pgvector:pg16` (Docker) hoặc cài thêm theo <https://github.com/pgvector/pgvector#installation>.

### `Jwt section missing` khi start app

→ `appsettings.json` thiếu section `Jwt`. Copy section `Jwt` mẫu vào, hoặc set qua env var `Jwt__Secret`.

### `Unable to find package Microsoft.EntityFrameworkCore` khi `dotnet restore`

→ Chưa tải đúng version .NET 9 SDK. Verify `dotnet --version` ≥ 9.0.x.

### `relation "users" does not exist` khi gọi API

→ Schema chưa được tạo. Chạy file init schema:
`psql -h localhost -U salio -d salio_dev -f db/migrations/2026_05_18_000_init_schema.sql`
hoặc (cách EF) `dotnet ef database update` + `psql -f db/migrations/2026_05_18_001_add_base_fields.sql`.

### Swagger UI báo `Failed to load API definition`

→ Build có warning XML doc nhưng vẫn chạy. Mở console log xem error chi tiết. Thường do thiếu `[ApiVersion("1.0")]` ở controller mới hoặc xung đột schema (đổi tên DTO cho khỏi trùng).

### `DbUpdateConcurrencyException` khi UPDATE

→ Đó là **optimistic locking** đang hoạt động đúng — bản ghi đã bị user khác sửa. FE cần refetch + thử lại. Đọc thêm về cột `version` (xmin) trong [`docs/database-schema.md`](docs/database-schema.md).

### Port 5080 / 7080 đã bị chiếm

→ Đổi port trong `src/Salio.Api/Properties/launchSettings.json` hoặc set env `ASPNETCORE_URLS=http://localhost:5090`.

---

## 11. Tài liệu bổ sung

| Tài liệu | Nội dung |
|---|---|
| [`docs/api-reference.md`](docs/api-reference.md) | Tất cả endpoint v1 — method, path, request body, response shape, permission. |
| [`docs/database-schema.md`](docs/database-schema.md) | Mô tả 40+ bảng — STT, kiểu DB, bắt buộc, mô tả; gồm mục **Base Fields System** chuẩn. |
| [`db/migrations/2026_05_18_000_init_schema.sql`](db/migrations/2026_05_18_000_init_schema.sql) | **Init schema** — tạo toàn bộ 46 bảng + index + trigger + extension trong 1 lần (khuyến nghị cho dev mới). |
| [`db/migrations/2026_05_18_001_add_base_fields.sql`](db/migrations/2026_05_18_001_add_base_fields.sql) | Migration SQL bổ sung 10 base fields cho bảng được tạo bằng EF migrations (cách thay thế). |
| `docker-compose.yml` | Postgres 16 + pgvector cho dev. |

---

## Hỗ trợ

- Issue / câu hỏi nội bộ: ping team `#salio-backend`.
- Bug ngoài team: tạo issue trên repo này, mô tả: phiên bản .NET, log lỗi, cách reproduce.
