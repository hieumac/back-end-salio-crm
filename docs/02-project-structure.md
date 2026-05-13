# 02 — Cấu trúc project

## Sơ đồ thư mục

```
backend-dotnet/
├── Salio.sln                          # Visual Studio solution
├── Directory.Build.props              # Shared MSBuild props (net9.0, nullable, etc.)
├── docker-compose.yml                 # PostgreSQL 16 + pgvector
├── README.md
├── .gitignore
│
├── docs/                              # Tài liệu này
│   ├── 00-overview.md
│   ├── 01-clean-architecture.md
│   ├── 02-project-structure.md       ← bạn đang đọc
│   └── ...
│
├── src/
│   ├── Salio.Domain/                  # 1️⃣  Domain layer
│   │   ├── Common/
│   │   │   ├── BaseEntity.cs         # BaseEntity, AuditableEntity, SoftDeletableEntity, TenantEntity
│   │   │   ├── Result.cs             # Result<T>, PagedResult<T>
│   │   │   └── DomainException.cs    # DomainException + NotFound/Forbidden/Validation/Conflict
│   │   ├── Enums/
│   │   │   └── Enums.cs              # 19 enums
│   │   └── Entities/
│   │       ├── Identity/             # Organization, User, OrgMember
│   │       ├── Auth/                 # AuthIdentity, UserSession, RefreshToken, MFA, ApiKey, Invitation
│   │       ├── Rbac/                 # SystemFunction, SystemAction, FunctionAction, Permission, Role, RolePermission, UserRole, PermissionGrant, Team, TeamMember
│   │       ├── Crm/                  # Company, Contact, Pipeline, PipelineStage, Deal, DealActivity, DealStageHistory, Product, DealProduct, DealFollower
│   │       ├── Models/               # Task (đặt trong namespace Models để tránh System.Threading.Tasks.Task)
│   │       ├── Duplicate/            # DuplicateMatchGroup, DuplicateMatchRecord
│   │       ├── Ai/                   # AiInsight, AiScoreHistory
│   │       ├── Library/              # LibraryNode, LibraryPermission, DocumentChunk
│   │       ├── Chat/                 # ChatConversation, ChatMessage, ChatMessageSource
│   │       └── Cross/                # Notification, AuditLog
│   │
│   ├── Salio.Application/             # 2️⃣  Application layer (use cases)
│   │   ├── Common/
│   │   │   ├── Interfaces/
│   │   │   │   ├── ISalioDbContext.cs        # DbSet ports
│   │   │   │   └── ICurrentUserService.cs    # + IJwtTokenService, IPasswordHasher, IPermissionChecker, IDateTimeProvider
│   │   │   └── Behaviors/
│   │   │       ├── LoggingBehavior.cs        # MediatR pipeline behavior
│   │   │       └── ValidationBehavior.cs
│   │   ├── DependencyInjection.cs           # AddApplication() — register MediatR + FluentValidation
│   │   └── Features/                         # Use cases group theo module
│   │       ├── Auth/
│   │       │   └── Commands/
│   │       │       ├── LoginCommand.cs
│   │       │       ├── RegisterCommand.cs
│   │       │       └── RefreshTokenCommand.cs
│   │       └── Deals/
│   │           ├── Commands/
│   │           │   ├── CreateDealCommand.cs
│   │           │   └── MoveDealStageCommand.cs
│   │           ├── Queries/
│   │           │   ├── ListDealsQuery.cs
│   │           │   └── GetDealByIdQuery.cs
│   │           └── Dtos/
│   │               └── DealDto.cs
│   │
│   ├── Salio.Infrastructure/          # 3️⃣  Infrastructure (EF Core, JWT, hash, …)
│   │   ├── DependencyInjection.cs           # AddInfrastructure() — register DbContext + services
│   │   ├── Configuration/
│   │   │   └── JwtOptions.cs                # POCO bind từ appsettings
│   │   ├── Persistence/
│   │   │   ├── SalioDbContext.cs            # EF Core DbContext, implement ISalioDbContext
│   │   │   ├── SalioDbSeeder.cs             # Seed functions/actions/permissions/roles
│   │   │   ├── Configurations/              # IEntityTypeConfiguration<T> — chia theo module
│   │   │   │   ├── IdentityConfigurations.cs
│   │   │   │   ├── AuthConfigurations.cs
│   │   │   │   ├── RbacConfigurations.cs
│   │   │   │   ├── CrmConfigurations.cs
│   │   │   │   └── RemainingConfigurations.cs  (Duplicate, Ai, Library, Chat, Cross)
│   │   │   └── Migrations/                  # EF generated (chưa scaffold)
│   │   └── Services/
│   │       ├── BCryptPasswordHasher.cs      # IPasswordHasher
│   │       ├── JwtTokenService.cs           # IJwtTokenService
│   │       └── PermissionChecker.cs         # IPermissionChecker
│   │
│   └── Salio.Api/                     # 4️⃣  HTTP layer
│       ├── Program.cs                       # Bootstrap (DI, middleware, swagger, jwt, versioning)
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── Properties/
│       │   └── launchSettings.json
│       ├── Common/
│       │   ├── ApiResponse.cs               # { success, data, error, traceId }
│       │   └── Authorization/
│       │       └── RequirePermissionAttribute.cs
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddleware.cs
│       ├── Services/
│       │   └── CurrentUserService.cs        # ICurrentUserService impl (đọc HttpContext)
│       └── Controllers/
│           ├── ApiControllerBase.cs         # Route convention chung
│           └── V1/                          # Versioned controllers
│               ├── AuthController.cs
│               ├── DealsController.cs
│               ├── SystemFunctionsController.cs
│               └── HealthController.cs
│
└── tests/                             # (chưa scaffold)
    ├── Salio.Domain.Tests/
    ├── Salio.Application.Tests/
    └── Salio.Api.IntegrationTests/
```

## Quy ước đặt tên

| Đối tượng | Quy ước | Ví dụ |
|---|---|---|
| Project | `Salio.<Layer>` | `Salio.Application` |
| Namespace | Khớp với folder | `Salio.Application.Features.Deals.Commands` |
| Entity class | Singular, PascalCase | `Deal`, `Company`, `PipelineStage` |
| Table DB | Plural, snake_case | `deals`, `companies`, `pipeline_stages` |
| Column DB | snake_case | `org_id`, `created_at`, `ai_score` |
| Command | `{Verb}{Entity}Command` | `CreateDealCommand`, `MoveDealStageCommand` |
| Query | `{Verb}{Entity}Query` | `ListDealsQuery`, `GetDealByIdQuery` |
| Handler | `{CommandName}Handler` | `CreateDealCommandHandler` |
| DTO | `{Entity}Dto` / `{Entity}ListItemDto` | `DealDto`, `DealListItemDto` |
| Validator | `{CommandName}Validator` | `CreateDealCommandValidator` |
| Controller | `{Resource}Controller` (plural) | `DealsController`, `CompaniesController` |

## File responsibilities

### `Directory.Build.props`

Áp dụng cho tất cả `.csproj` trong solution:

- `<TargetFramework>net9.0</TargetFramework>`
- `<Nullable>enable</Nullable>` — bật nullable reference types
- `<ImplicitUsings>enable</ImplicitUsings>` — global usings auto
- `<GenerateDocumentationFile>true</GenerateDocumentationFile>` — sinh XML doc cho Swagger

### `Salio.sln`

Liệt kê 4 project trong solution folder `src`.

### `docker-compose.yml`

PostgreSQL 16 + pgvector ở port 5432, user/pass `salio/salio123`, volume `salio-pgdata` để giữ data giữa các lần restart.

### `.gitignore`

Bỏ qua `bin/`, `obj/`, `*.log`, `appsettings.Local.json`, `.env`.

## Mở rộng cấu trúc

### Thêm module mới (ví dụ: `Reports`)

```
src/Salio.Application/Features/Reports/
├── Queries/
│   ├── GetSalesReportQuery.cs
│   └── GetPipelineReportQuery.cs
└── Dtos/
    └── ReportDto.cs

src/Salio.Api/Controllers/V1/ReportsController.cs
```

### Thêm version mới (v2)

```
src/Salio.Api/Controllers/V2/
└── DealsController.cs    # [ApiVersion("2.0")]
```

Controller v2 vẫn dùng cùng MediatR handler — chỉ khác shape của DTO/route nếu cần.
