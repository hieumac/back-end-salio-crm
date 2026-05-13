# 08 — Database setup

> PostgreSQL 16 + pgvector. Hướng dẫn dựng DB, chạy migration, seed, troubleshoot.

## Yêu cầu

- Docker + Docker Compose (khuyến nghị) **hoặc** PostgreSQL 16 cài thẳng
- Extension: `uuid-ossp`, `pgvector`, `pg_trgm` (đã có trong image `pgvector/pgvector:pg16`)
- .NET SDK 9.0+

## Chạy DB bằng Docker

```bash
cd backend-dotnet
docker compose up -d
```

`docker-compose.yml`:

```yaml
services:
  postgres:
    image: pgvector/pgvector:pg16
    container_name: salio-postgres
    environment:
      POSTGRES_DB: salio
      POSTGRES_USER: salio
      POSTGRES_PASSWORD: salio123
    ports:
      - "5432:5432"
    volumes:
      - salio-pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U salio -d salio"]
      interval: 5s
      timeout: 5s
      retries: 5

volumes:
  salio-pgdata:
```

Check container:

```bash
docker ps                       # phải thấy salio-postgres (healthy)
docker logs salio-postgres      # xem log nếu lỗi
docker exec -it salio-postgres psql -U salio -d salio
```

Dừng + giữ data: `docker compose down`.
Dừng + xóa data: `docker compose down -v`.

## Connection string

`appsettings.json`:

```json
"ConnectionStrings": {
  "Default": "Host=localhost;Port=5432;Database=salio;Username=salio;Password=salio123;Include Error Detail=true"
}
```

`appsettings.Development.json` override `Database=salio_dev`.

Production: dùng env var `ConnectionStrings__Default` (Linux) hoặc User Secrets / KeyVault.

## EF Core CLI

Cài tool (chỉ 1 lần/máy):

```bash
dotnet tool install --global dotnet-ef
```

Sinh migration:

```bash
cd backend-dotnet
dotnet ef migrations add Initial \
  --project src/Salio.Infrastructure \
  --startup-project src/Salio.Api \
  --output-dir Persistence/Migrations
```

Áp dụng migration thủ công:

```bash
dotnet ef database update \
  --project src/Salio.Infrastructure \
  --startup-project src/Salio.Api
```

Sinh script SQL (cho prod):

```bash
dotnet ef migrations script \
  --project src/Salio.Infrastructure \
  --startup-project src/Salio.Api \
  --output ./migrations.sql --idempotent
```

Xóa migration cuối (chưa apply):

```bash
dotnet ef migrations remove \
  --project src/Salio.Infrastructure \
  --startup-project src/Salio.Api
```

## Auto-migrate khi app khởi động

`appsettings.json`:

```json
"Database": {
  "AutoMigrate": true,
  "AutoSeed": true
}
```

`Program.cs`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SalioDbContext>();
    if (config.GetValue<bool>("Database:AutoMigrate"))
        await db.Database.MigrateAsync();
    if (config.GetValue<bool>("Database:AutoSeed"))
        await SalioDbSeeder.SeedAsync(db);
}
```

> Production khuyến nghị **tắt AutoMigrate**, chạy migration bằng pipeline CI/CD để control rollback.

## Seeder

`SalioDbSeeder.SeedAsync(db)` chạy idempotent — gọi nhiều lần không trùng.

Insert theo thứ tự:
1. 15 SystemAction (view/create/update/...)
2. 30 SystemFunction (dashboard.*, crm.*, ai.*, ...)
3. FunctionAction (ma trận function × action hợp lệ)
4. Permission (`function_id + action_id + scope=Any`)
5. 6 system Role + RolePermission (theo scope pattern: `*`, `crm.*`, `crm.deals.list:view`)

Sau seed:
- `select count(*) from system_actions;`   → 15
- `select count(*) from system_functions;` → 30
- `select count(*) from permissions;`      → ~ 200+ (tùy FunctionAction)
- `select count(*) from roles where is_system = true;` → 6

## Schema overview

46 bảng chia 9 module, schema mặc định `public`. Migration history: `__ef_migrations`.

| Module | Bảng chính |
|---|---|
| Identity | organizations, users, org_members |
| Auth | auth_identities, user_sessions, refresh_tokens, mfa_factors, login_attempts, api_keys, invitations, email_verification_tokens, password_reset_tokens, mfa_challenges |
| RBAC | system_functions, system_actions, function_actions, permissions, roles, role_permissions, user_roles, permission_grants, teams, team_members |
| CRM | companies, contacts, pipelines, pipeline_stages, deals, deal_activities, deal_stage_history, products, deal_products, deal_followers |
| Models | tasks |
| Duplicate | duplicate_match_groups, duplicate_match_records |
| AI | ai_insights, ai_score_history |
| Library | library_nodes, library_permissions, document_chunks (vector(1536)) |
| Chat | chat_conversations, chat_messages, chat_message_sources |
| Cross | notifications, audit_logs |

## Indexes & performance

Quy ước trong EntityConfiguration:

```csharp
b.HasIndex(x => new { x.OrgId, x.Code }).IsUnique();
b.HasIndex(x => new { x.OrgId, x.StageId });
```

- Hầu hết entity tenant có index `(OrgId, ...)` để DB query nhanh trong context org.
- `pg_trgm` extension cho ILike search trên text (Companies.Name, Contacts.FullName).
- `vector(1536)` cho embedding — sử dụng cosine distance qua Npgsql.

### Tạo HNSW index cho embedding (manual)

EF Core chưa hỗ trợ tạo HNSW index → tạo bằng migration thuần SQL:

```sql
CREATE INDEX ON document_chunks USING hnsw (embedding vector_cosine_ops);
```

Hoặc IVFFlat:

```sql
CREATE INDEX ON document_chunks USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);
```

Thêm vào migration:

```csharp
migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_document_chunks_embedding ON document_chunks USING hnsw (embedding vector_cosine_ops);");
```

## Soft delete & query filter

Mọi entity kế thừa `SoftDeletableEntity` (gồm `TenantEntity`) đều có:

```csharp
b.HasQueryFilter(x => x.DeletedAt == null);
```

→ truy vấn EF mặc định bỏ qua bản ghi đã xóa mềm.

Để truy vấn cả deleted: `db.Deals.IgnoreQueryFilters().Where(...)`.

`DbContext.SaveChangesAsync` tự intercept:

```csharp
if (entry.State == EntityState.Deleted)
{
    entry.State = EntityState.Modified;
    entry.Entity.DeletedAt = DateTimeOffset.UtcNow;
}
```

## Row-level security (tương lai)

Hiện multi-tenant qua app code (filter `OrgId` trong handler). Để bật RLS thật:

```sql
ALTER TABLE deals ENABLE ROW LEVEL SECURITY;
CREATE POLICY deals_tenant_isolation ON deals
  USING (org_id = current_setting('app.current_org_id')::uuid);
```

Trong `DbContext.OnConfiguring` set per-connection:

```csharp
await conn.ExecuteAsync($"SET app.current_org_id = '{orgId}'");
```

→ DB tự ép isolation kể cả khi handler quên filter.

## Backup & restore

Backup (Docker):

```bash
docker exec salio-postgres pg_dump -U salio -d salio -F c -f /tmp/salio.dump
docker cp salio-postgres:/tmp/salio.dump ./backups/$(date +%Y%m%d).dump
```

Restore:

```bash
docker cp ./backups/2026-05-13.dump salio-postgres:/tmp/restore.dump
docker exec salio-postgres pg_restore -U salio -d salio --clean --if-exists /tmp/restore.dump
```

## Troubleshoot

| Lỗi | Nguyên nhân | Fix |
|---|---|---|
| `extension "vector" is not available` | Dùng image `postgres:16` thay vì `pgvector/pgvector:pg16` | Đổi image trong compose, recreate container |
| `relation "salio.__ef_migrations" does not exist` | Lần đầu chạy mà tắt AutoMigrate | Bật AutoMigrate hoặc chạy `dotnet ef database update` |
| `password authentication failed` | Sai connection string | Check `Username=salio;Password=salio123` |
| `column "embedding" cannot be cast to type vector` | DB cũ chưa có extension `vector` | `CREATE EXTENSION vector;` thủ công |
| Migration báo `column already exists` | Schema lệch giữa EF model và DB | Drop DB và migrate lại (dev) / sửa migration (prod) |

## Reset DB lúc dev

```bash
docker compose down -v          # xóa volume
docker compose up -d            # tạo mới
dotnet run --project src/Salio.Api  # auto-migrate + seed
```
