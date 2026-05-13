# 00 — Tổng quan Salio Backend

> Backend C# .NET 9 cho dự án **Salio Sales AI** — một CRM B2B có pipeline Kanban, AI scoring, AI chat (RAG), document library và RBAC theo function-based permissions.

## Mục tiêu

Backend đáp ứng:

- **Multi-tenant**: nhiều tổ chức (organization) trên cùng một instance, dữ liệu cô lập qua `org_id` + Row Level Security (PostgreSQL).
- **REST API versioned**: chuẩn `/api/v{version}/...`, dễ tiến hóa song song nhiều phiên bản.
- **CQRS** (Command/Query Responsibility Segregation) qua MediatR — tách rõ command/write và query/read.
- **Function-based RBAC** — phân quyền theo từng chức năng UI × hành động × phạm vi (scope), không phải chuỗi permission tự do.
- **AI-ready**: pgvector cho embeddings, RAG chat, AI scoring cho deals, AI insights.
- **Audit + soft delete + activity log** sẵn sàng.

## Tech stack

| Hạng mục | Lựa chọn | Ghi chú |
|---|---|---|
| Runtime | **.NET 9** | LTS preview, latest C# 13 |
| Database | **PostgreSQL 16** + pgvector | Multi-tenant, full-text, vector search |
| ORM | **EF Core 9** + Npgsql | Code-first, migrations |
| API | ASP.NET Core minimal hosting + MVC controllers | |
| API Versioning | `Asp.Versioning.Mvc` | URL segment + header |
| CQRS | **MediatR 12** | Pipeline behaviors (logging, validation) |
| Validation | **FluentValidation 11** | Auto-discover validators |
| Mapping | **Mapster** | Source-gen mapper |
| Auth | **JWT Bearer** + BCrypt | Access + refresh token rotation |
| Logging | **Serilog** | Console + File rolling |
| Docs | **Swashbuckle** (Swagger) | OpenAPI 3 |

## Frontend mà backend này phục vụ

- Vue 3 + TypeScript + Tailwind CSS v4 + shadcn-vue
- Đường dẫn frontend: `C:\Users\Admin\Documents\front-end-sale-ai`
- Response envelope khớp với type `ApiResponse<T>` trong `src/types/common.ts`.

## Các tài liệu liên quan

| File | Nội dung |
|---|---|
| `01-clean-architecture.md` | Layer & dependency direction |
| `02-project-structure.md` | Sơ đồ thư mục, file responsibilities |
| `03-domain-entities.md` | 46 entities, 19 enums, base classes |
| `04-application-layer.md` | CQRS, validators, ports |
| `05-infrastructure-layer.md` | DbContext, EF configurations, services |
| `06-api-layer.md` | Controllers, middleware, Program.cs |
| `07-api-endpoints.md` | Tất cả endpoints v1, conventions |
| `08-database-setup.md` | Migrations, seed, docker, RLS |
| `09-authentication.md` | Login/Refresh/Register flow |
| `10-authorization-rbac.md` | Function-action-scope permissions |
| `11-development-guide.md` | Quy trình thêm endpoint, entity, permission |
| `12-deployment.md` | Production checklist |

## Trạng thái hiện tại

- Solution + 4 projects scaffold xong, references đúng theo Clean Architecture.
- 46 domain entities + 19 enums.
- EF Core configurations cho mọi entity, indexes, soft delete, query filters.
- Auth: Register / Login / Refresh token (rotation).
- CRM: List/Get/Create Deal + Move stage.
- RBAC: function/action/permission/role + seeder cho 30 functions, 15 actions, 6 system roles.
- Cross-cutting: exception middleware, logging behavior, validation behavior.
- Swagger UI + JWT bearer.
- Docker compose cho PostgreSQL + pgvector.

## Bước tiếp theo (gợi ý)

1. Implement đầy đủ CRUD cho Company / Contact / Product / Pipeline / Stage / Task.
2. Implement Library + Chat (RAG) endpoints.
3. Implement AI scoring + AI insights endpoints.
4. Implement Duplicate detection.
5. Thêm Audit log auto-write qua EF interceptor.
6. Implement RLS policies trong migration thay vì chỉ dựa vào query filter.
7. Thêm test projects (Domain unit, Application handler, API integration).
8. Thiết lập CI/CD (build, test, migration check).
