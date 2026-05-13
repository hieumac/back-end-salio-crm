# 03 — Domain layer

> 46 entities + 19 enums chia theo 9 module: Identity, Auth, RBAC, CRM, Models, Duplicate, AI, Library, Chat, Cross-cutting.

## Base classes (Common/)

| Class | Field | Mục đích |
|---|---|---|
| `BaseEntity` | `Guid Id` | Khóa chính UUID v4 |
| `AuditableEntity : BaseEntity` | `+ CreatedAt, UpdatedAt` | Tự cập nhật bởi `SalioDbContext.SaveChangesAsync` |
| `SoftDeletableEntity : AuditableEntity` | `+ DeletedAt, IsDeleted` | Soft delete — `Delete` → set `DeletedAt` |
| `TenantEntity : SoftDeletableEntity` | `+ OrgId` | Entity thuộc một tổ chức — mọi truy vấn phải filter theo `OrgId` |

## Result & exceptions

```csharp
public class Result { bool IsSuccess; string? Error; string? Code; }
public class Result<T> : Result { T? Value; }
public class PagedResult<T> { Items, Page, PageSize, Total, TotalPages; }

public class DomainException(string message, string? code) : Exception;
public class NotFoundException(string resource, object? key);
public class ForbiddenException(string action);
public class ValidationException(string message);
public class ConflictException(string message);
```

Handler throw → `ExceptionHandlingMiddleware` bắt → trả JSON chuẩn (404/403/422/409/400).

## Enums (19)

| Enum | Giá trị |
|---|---|
| `OrgRole` | Owner, Admin, Manager, Sales, Viewer |
| `DealSource` | Inbound, Outbound, Referral, Marketing, Event, Other |
| `TaskStatus` | Pending, InProgress, Done, Canceled |
| `TaskPriority` | Low, Medium, High, Urgent |
| `DupConfidence` | High, Medium, Low |
| `DupStatus` | Pending, Resolved, Ignored |
| `LibraryNodeType` | Folder, File, Document, Note |
| `LibraryStatus` | Draft, Active, Archived |
| `LibraryRootType` | Company, Personal, Shared |
| `ChatRole` | User, Assistant, System, Tool |
| `AiInsightStatus` | Active, Dismissed, Acted, Expired |
| `AuthProvider` | Password, Google, Microsoft, Apple, Saml, Oidc |
| `MfaType` | Totp, Sms, Email, WebAuthn, RecoveryCode |
| `LoginResult` | Success, InvalidCredentials, Locked, MfaRequired, Disabled |
| `GrantEffect` | Allow, Deny |
| `PermissionScope` | Own, Assigned, Team, Any |
| `TeamRoleType` | Lead, Member |
| `SystemModuleGroup` | Dashboard, Crm, Ai, Library, Reports, Settings, System |
| `FunctionRiskLevel` | Low, Medium, High, Critical |

Tất cả map xuống Postgres `varchar` qua `.HasConversion<string>()` để dễ đọc khi query DB.

## Identity module (3 entities)

```
Organization 1 ──< OrgMember >── 1 User
```

| Entity | Field chính |
|---|---|
| `Organization : AuditableEntity` | `Name, Slug (UK), Plan, Locale, Settings (jsonb)` |
| `User : SoftDeletableEntity` | `Email (UK), FullName, AvatarUrl, LastLoginAt, IsActive, EmailVerified` |
| `OrgMember : AuditableEntity` | `OrgId, UserId, Title, IsActive, JoinedAt` — vai trò cụ thể quản lý qua `UserRole` (RBAC) |

> Mật khẩu **không** ở User mà ở `AuthIdentity` (provider=Password) — vì một user có thể login bằng nhiều provider.

## Auth module (9 entities)

| Entity | Mục đích |
|---|---|
| `AuthIdentity` | Mỗi provider 1 record (password/google/microsoft/...) — chứa `PasswordHash` chỉ khi provider=Password |
| `UserSession` | Phiên đăng nhập, gắn IP/UserAgent/DeviceFingerprint, có `ExpiresAt`, `RevokedAt` |
| `RefreshToken` | Refresh token rotation — `TokenHash`, `SessionId`, `ReplacedByTokenId` |
| `EmailVerificationToken` | Token xác thực email |
| `PasswordResetToken` | Token quên mật khẩu |
| `MfaFactor` | TOTP / SMS / Email / WebAuthn / RecoveryCode |
| `MfaChallenge` | Code đang chờ verify, `Attempts`, `ExpiresAt` |
| `LoginAttempt` | Log mọi lần đăng nhập thành/bại — anti brute force |
| `ApiKey` | API key cho integration (M2M) |
| `Invitation` | Mời user vào org |

## RBAC module (10 entities)

```
SystemFunction ──< FunctionAction >── SystemAction
       │                                    │
       └───────────< Permission >───────────┘
                        │
                        ├── RolePermission ── Role ── UserRole ── User
                        └── PermissionGrant ────────────────────── User
```

| Entity | Mục đích |
|---|---|
| `SystemFunction` | Chức năng UI (`crm.deals.kanban`, `ai.chat`...) — code, name, ModuleGroup, RiskLevel |
| `SystemAction` | Hành động chuẩn (`view`, `create`, `update`, `delete`...) |
| `FunctionAction` | Ma trận function × action — quy định hành động nào hợp lệ trên function nào |
| `Permission` | `function_id + action_id + scope` (auto-gen từ FunctionAction) — code dạng `crm.deals.list:create:any` |
| `Role` | Vai trò (system role hoặc custom). System roles: super_admin, owner, admin, manager, sales, viewer |
| `RolePermission` | M-M giữa Role và Permission |
| `UserRole` | Gán role cho user trong context một org, có `ExpiresAt` |
| `PermissionGrant` | Grant trực tiếp permission cho user, có `Effect = Allow | Deny`, override RolePermission |
| `Team` | Team trong org, có parent (hierarchy), `Manager` |
| `TeamMember` | M-M giữa Team và User, `RoleType = Lead | Member` |

Xem chi tiết logic resolve permission ở `10-authorization-rbac.md`.

## CRM module (10 entities)

```
Pipeline ──< PipelineStage >── < Deal >── Company
                                 │  │
                                 │  └── Contact (primary contact)
                                 │
                                 ├── DealActivity (log)
                                 ├── DealStageHistory (chuyển stage)
                                 ├── DealProduct ── Product
                                 ├── Task
                                 └── DealFollower ── User
```

| Entity | Field chính |
|---|---|
| `Company : TenantEntity` | Name, TaxCode, Industry, Size, Website, Phone, Email, Address, OwnerId, CustomFields (jsonb) |
| `Contact : TenantEntity` | CompanyId?, FullName, Email, Phone, Title, IsPrimary, CustomFields |
| `Pipeline : TenantEntity` | Name, IsDefault, Order |
| `PipelineStage` | PipelineId, Code, Name, Order, DefaultProbability, IsWon, IsLost, Color |
| `Deal : TenantEntity` | Code (UK trong org), Title, PipelineId, StageId, Value, Currency, Probability, Source, CompanyId?, ContactId?, AssigneeId?, ExpectedCloseDate?, AiScore?, LastActivityAt? |
| `DealActivity` | DealId, Type (`deal_created` / `stage_changed` / `note_added`...), Title, Description, Metadata (jsonb), ActorId |
| `DealStageHistory` | DealId, FromStageId?, ToStageId, DurationInPrevStageSeconds, ChangedById |
| `Product : TenantEntity` | Code, Name, UnitPrice, Unit, Currency |
| `DealProduct` | DealId, ProductId, Quantity, UnitPrice, DiscountPct, Total |
| `DealFollower` (composite PK) | DealId + UserId — user theo dõi deal |

## Models module (1 entity)

| Entity | Mục đích |
|---|---|
| `Task : TenantEntity` | Đặt trong `Salio.Domain.Entities.Models` để tránh conflict với `System.Threading.Tasks.Task`. Field: Title, Description, AssigneeId?, DealId?, DueAt, CompletedAt, Priority, Status |

## Duplicate module (2 entities)

| Entity | Mục đích |
|---|---|
| `DuplicateMatchGroup : TenantEntity` | Nhóm các record nghi trùng — EntityType, MatchField, Confidence, Status, MasterRecordId? |
| `DuplicateMatchRecord` | Record cụ thể trong nhóm, lưu `RecordSnapshot` (jsonb) tại thời điểm phát hiện |

## AI module (2 entities)

| Entity | Mục đích |
|---|---|
| `AiInsight : TenantEntity` | Gợi ý/cảnh báo từ AI — ScopeType (deal/company/...), ScopeId, Type, Title, Body, Priority, SuggestedAction (jsonb), Status |
| `AiScoreHistory` | Lịch sử AI score cho deal — Score, Reasons (jsonb), Model |

## Library module (3 entities, có pgvector)

```
LibraryNode (cây folder) ──< LibraryPermission
        │
        └──< DocumentChunk (chunked + embedded)
```

| Entity | Mục đích |
|---|---|
| `LibraryNode : TenantEntity` | Cây folder/file — ParentId, RootType (Company/Personal/Shared), Type (Folder/File/Document/Note), Status, FileId, FileUrl, FileMime, FileSizeBytes, Path, OwnerId |
| `LibraryPermission` | NodeId, PrincipalType (user/team/role), PrincipalId, Permission (view/edit/manage) |
| `DocumentChunk` | NodeId, OrgId, ChunkIndex, Content, ContentTokens, **Embedding (vector(1536))**, Metadata (jsonb) |

## Chat module (3 entities)

| Entity | Mục đích |
|---|---|
| `ChatConversation : TenantEntity` | UserId, Title, ContextType, ContextId, Pinned, LastMessageAt |
| `ChatMessage` | ConversationId, Role (user/assistant/system/tool), Content, ContentTokens, Model, LatencyMs, Metadata |
| `ChatMessageSource` | Message cite chunk nào — MessageId, ChunkId, Score, Label (RAG citation) |

## Cross-cutting (2 entities)

| Entity | Mục đích |
|---|---|
| `Notification : AuditableEntity` | OrgId, RecipientId, Type, Title, Body, LinkUrl, EntityType, EntityId, ReadAt |
| `AuditLog : AuditableEntity` | OrgId, ActorId, Action, EntityType, EntityId, Before (jsonb), After (jsonb), IpAddress, UserAgent |

## Tổng kết counts

| Module | Entities |
|---|---|
| Identity | 3 |
| Auth | 9 |
| RBAC | 10 |
| CRM | 10 |
| Models | 1 |
| Duplicate | 2 |
| AI | 2 |
| Library | 3 |
| Chat | 3 |
| Cross | 2 |
| **Total** | **45** |

_(+1 enum nội bộ chia sẻ → khớp Prisma schema 46 models)_
