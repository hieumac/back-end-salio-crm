# Salio CRM — Database Schema

> Mô tả chi tiết các bảng của database `salio_dev` (PostgreSQL 15+/16/17 + extensions `uuid-ossp`, `vector` pgvector, `pg_trgm`). Tài liệu này được sinh từ `Salio.Domain.Entities` + cấu hình EF Core trong `Salio.Infrastructure.Persistence.Configurations`.

## Ký hiệu trong tài liệu

- **Tên trường**: dùng camelCase (theo style của entity C#); khi sinh DB thực tế, EF Core sẽ map sang snake_case nếu cấu hình `UseSnakeCaseNamingConvention()` — tham khảo `SalioDbContext` cho mapping chính xác.
- **Kiểu DB**: kiểu thật trong PostgreSQL (`uuid`, `varchar(N)`, `text`, `int`, `bigint`, `numeric(P,S)`, `timestamptz`, `date`, `boolean`, `jsonb`, `vector(N)`).
- **Bắt buộc**: `true` = NOT NULL; `false` = nullable.
- **PK / FK / UQ / IX**: viết tắt cho khoá chính / khoá ngoại / unique index / index thường.
- **TenantEntity**: kế thừa từ `TenantEntity` ⇒ luôn có `orgId` + soft-delete (`deletedAt`).

---

## Phần 1 — Hệ kế thừa (Base Entities)

| Base | Trường thêm | Ghi chú |
|---|---|---|
| `BaseEntity` | `id: uuid` | PK toàn hệ thống. Dùng cho junction table có composite key. |
| `AuditableEntity` | + 8 base fields (xem dưới) | Mọi entity nghiệp vụ đều kế thừa từ đây trở xuống. |
| `SoftDeletableEntity` | + `deletedAt`, `deletedBy` | Soft delete; áp dụng global query filter `deletedAt IS NULL`. |
| `TenantEntity` | + `orgId: uuid` | Multi-tenant — mọi truy vấn phải lọc theo `orgId`. |

---

## Phần 2 — Base Fields System (10 trường chuẩn cho mọi bảng)

Mỗi bảng nghiệp vụ (trừ junction table có composite key) đều có **10 trường audit/control chuẩn** sau. Trong các bảng cụ thể bên dưới, các trường này **không liệt kê lại** — chỉ liệt kê trường nghiệp vụ riêng.

| STT | Tên trường | Kiểu DB | Bắt buộc | Default | Mô tả |
|---|---|---|---|---|---|
| 1 | `id` | uuid | true | `uuid_generate_v4()` | Khóa chính. |
| 2 | `created_at` | timestamptz | true | `CURRENT_TIMESTAMP` | Thời điểm tạo bản ghi (UTC). |
| 3 | `created_by` | uuid | false | NULL | UserId người tạo — FK `users(id)`. |
| 4 | `updated_at` | timestamptz | true | `CURRENT_TIMESTAMP` | Thời điểm cập nhật gần nhất; trigger tự cập nhật. |
| 5 | `updated_by` | uuid | false | NULL | UserId người cập nhật gần nhất — FK `users(id)`. |
| 6 | `deleted_at` | timestamptz | false | NULL | Soft delete: thời điểm xóa mềm. NULL = còn hiệu lực. (chỉ `SoftDeletableEntity` / `TenantEntity`) |
| 7 | `deleted_by` | uuid | false | NULL | UserId thực hiện xóa mềm — FK `users(id)`. (chỉ soft-delete) |
| 8 | `is_active` | boolean | true | `TRUE` | Bật/tắt nhanh trạng thái hoạt động (không phải soft-delete). |
| 9 | `sort_index` | integer | true | `0` | Thứ tự sắp xếp UI (drag & drop). |
| 10 | `version` | xid (xmin) | true | hệ thống | Cột system `xmin` của PostgreSQL — phục vụ optimistic locking (EF Core `[Timestamp]`). |

**Ý nghĩa trong vận hành:**

- *Audit trail* — `created_at/by` + `updated_at/by` truy vết hoạt động; bắt buộc cho compliance, debug, rollback.
- *Soft delete* — `deleted_at/by` giữ data để phục hồi/báo cáo; kết hợp partial index `WHERE deleted_at IS NULL` giữ hiệu năng.
- *Optimistic locking* — `version` (xmin) tự thay đổi mỗi UPDATE; mismatch giữa lúc đọc và UPDATE → `DbUpdateConcurrencyException` → FE refetch.
- *is_active* — tách biệt với soft-delete: deactivate tạm thời (vd: account treo) khác với xóa hẳn.
- *sort_index* — chuẩn hóa drag-and-drop ở UI, tránh mỗi feature tự sáng tạo cách lưu thứ tự.

**DDL mẫu (template):**

```sql
CREATE TABLE example (
    id          uuid         PRIMARY KEY DEFAULT uuid_generate_v4(),
    -- ===== Trường nghiệp vụ chèn vào đây =====
    -- ===== Base fields (audit & control) =====
    created_at  timestamptz  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_by  uuid         NULL,
    updated_at  timestamptz  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by  uuid         NULL,
    deleted_at  timestamptz  NULL,
    deleted_by  uuid         NULL,
    is_active   boolean      NOT NULL DEFAULT TRUE,
    sort_index  integer      NOT NULL DEFAULT 0
    -- version  → cột system xmin của PostgreSQL (không cần khai báo)
);

COMMENT ON COLUMN example.id         IS 'Khóa chính UUID';
COMMENT ON COLUMN example.created_at IS 'Thời điểm tạo bản ghi (UTC)';
COMMENT ON COLUMN example.created_by IS 'UserId người tạo — FK users(id)';
COMMENT ON COLUMN example.updated_at IS 'Thời điểm cập nhật gần nhất; trigger tự cập nhật';
COMMENT ON COLUMN example.updated_by IS 'UserId người cập nhật gần nhất — FK users(id)';
COMMENT ON COLUMN example.deleted_at IS 'Xóa mềm: thời điểm bị xóa; NULL = còn hiệu lực';
COMMENT ON COLUMN example.deleted_by IS 'UserId người xóa mềm — FK users(id)';
COMMENT ON COLUMN example.is_active  IS 'Bật/tắt trạng thái hoạt động';
COMMENT ON COLUMN example.sort_index IS 'Vị trí sắp xếp UI (drag & drop)';

-- Trigger auto-update updated_at
CREATE TRIGGER trg_example_updated_at
BEFORE UPDATE ON example
FOR EACH ROW EXECUTE FUNCTION trg_set_updated_at();

-- Partial index — query nhanh các bản ghi còn hiệu lực
CREATE INDEX ix_example_active ON example (id)
WHERE deleted_at IS NULL AND is_active = TRUE;
```

**Bảng không áp dụng đầy đủ:**

- *Junction tables* (`function_actions`, `role_permissions`, `deal_followers`): chỉ có composite PK, **không** thêm base fields.
- *Log/history tables bất biến* (`audit_logs`, `login_attempts`, `deal_activities`, `deal_stage_history`, `chat_messages`, `ai_score_history`, `dup_match_records`): bỏ qua `deleted_at/by`, `sort_index`, `is_active` (chỉ giữ `created_at/by`).
- *Token tables* (`refresh_tokens`, `email_verification_tokens`, `password_reset_tokens`, `mfa_challenges`): bỏ qua `is_active`, `sort_index`, soft-delete — token dùng 1 lần.

Xem migration đầy đủ: [`db/migrations/2026_05_18_001_add_base_fields.sql`](../db/migrations/2026_05_18_001_add_base_fields.sql).

---

## Mục lục

1. [Identity & Organizations](#identity--organizations) — `organizations`, `users`, `org_members`
2. [Authentication & Sessions](#authentication--sessions) — `auth_identities`, `user_sessions`, `refresh_tokens`, `email_verification_tokens`, `password_reset_tokens`, `mfa_factors`, `mfa_challenges`, `login_attempts`, `api_keys`, `invitations`
3. [RBAC](#rbac) — `system_functions`, `system_actions`, `function_actions`, `permissions`, `roles`, `role_permissions`, `user_roles`, `permission_grants`, `teams`, `team_members`
4. [CRM Core](#crm-core) — `companies`, `contacts`, `pipelines`, `pipeline_stages`, `deals`, `deal_activities`, `deal_stage_history`, `products`, `deal_products`, `deal_followers`
5. [Tasks](#tasks-1) — `tasks`
6. [Duplicate Detection](#duplicate-detection-1) — `dup_match_groups`, `dup_match_records`
7. [AI](#ai-1) — `ai_insights`, `ai_score_history`
8. [Library & Document Chunks](#library--document-chunks) — `library_nodes`, `library_permissions`, `document_chunks`
9. [Chat](#chat-1) — `chat_conversations`, `chat_messages`, `chat_message_sources`
10. [Cross-Cutting](#cross-cutting-1) — `notifications`, `audit_logs`
11. [Khuyến nghị tối ưu kiểu dữ liệu](#khuyến-nghị-tối-ưu-kiểu-dữ-liệu) — chuyển enum string → tinyint, dùng bitmask cho flag

---

## Identity & Organizations

### `organizations`
Đại diện cho 1 tổ chức/tenant (mỗi org có dữ liệu CRM độc lập). User có thể thuộc nhiều org thông qua `org_members`.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | name | varchar(200) | true | Tên hiển thị; IX. |
| 3 | slug | varchar(80) | true | Định danh URL-safe, UQ toàn hệ thống. |
| 4 | plan | varchar(40) | true | Mặc định `"free"`; dùng cho billing/gating tính năng. |
| 5 | locale | varchar(10) | true | Ngôn ngữ mặc định (vd `"vi-VN"`). |
| 6 | settings | jsonb | false | Cấu hình tự do (theme, branding, feature flags…). |
| 7 | createdAt | timestamptz | true | |
| 8 | updatedAt | timestamptz | true | |

---

### `users`
User toàn cục (chưa gắn org). Quan hệ user ↔ org đặt ở `org_members`. Bảng có soft-delete (`deletedAt`).

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | email | varchar(200) | true | UQ toàn hệ thống. |
| 3 | fullName | varchar(200) | true | Tên hiển thị. |
| 4 | avatarUrl | varchar(500) | false | URL avatar. |
| 5 | lastLoginAt | timestamptz | false | Lần đăng nhập gần nhất. |
| 6 | isActive | boolean | true | Khoá tài khoản nếu `false`. |
| 7 | emailVerified | boolean | true | Đã verify email chưa. |
| 8 | createdAt | timestamptz | true | |
| 9 | updatedAt | timestamptz | true | |
| 10 | deletedAt | timestamptz | false | Soft delete. |

---

### `org_members`
Bảng nối user ↔ org; định danh "user X thuộc org Y với chức danh Z".

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | true | FK → organizations(id); cascade. UQ với userId. |
| 3 | userId | uuid | true | FK → users(id); cascade. UQ với orgId. |
| 4 | title | varchar(120) | false | Chức danh trong org (vd `"Sales Manager"`). |
| 5 | isActive | boolean | true | Đặt `false` để treo thành viên nhưng giữ lịch sử. |
| 6 | joinedAt | timestamptz | false | Thời điểm gia nhập org. |
| 7 | createdAt | timestamptz | true | |
| 8 | updatedAt | timestamptz | true | |

---

## Authentication & Sessions

### `auth_identities`
Mỗi user có thể có nhiều cách đăng nhập (password, Google, Microsoft, SAML…). Mỗi method = 1 row.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | userId | uuid | true | FK → users(id); cascade. |
| 3 | provider | varchar(20) | true | Enum `AuthProvider`: `Password/Google/Microsoft/Apple/Saml/Oidc`. **Khuyến nghị:** chuyển smallint, xem mục cuối. |
| 4 | providerUserId | varchar(200) | false | ID bên IdP (vd Google subject). UQ một phần với `provider` khi không null. |
| 5 | passwordHash | text | false | BCrypt hash; chỉ dùng khi `provider=Password`. |
| 6 | passwordChangedAt | timestamptz | false | Ép logout tất cả session khi đổi mật khẩu. |
| 7 | providerMetadata | jsonb | false | Token/claim của provider. |
| 8 | lastUsedAt | timestamptz | false | |
| 9 | createdAt | timestamptz | true | |
| 10 | updatedAt | timestamptz | true | |

---

### `user_sessions`
Mỗi lần đăng nhập tạo 1 session để track device/IP. Refresh token tham chiếu về session.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | userId | uuid | true | FK → users(id); cascade. |
| 3 | sessionToken | varchar(120) | true | UQ; opaque token gắn vào cookie session. |
| 4 | ipAddress | varchar(64) | false | IPv4/IPv6. |
| 5 | userAgent | varchar(500) | false | UA browser/app. |
| 6 | deviceFingerprint | varchar(200) | false | Hash fingerprint từ FE. |
| 7 | deviceName | varchar(120) | false | "Chrome on MacBook Pro". |
| 8 | lastActiveAt | timestamptz | false | Cập nhật mỗi request. |
| 9 | expiresAt | timestamptz | true | Hết hạn => yêu cầu login lại. |
| 10 | revokedAt | timestamptz | false | Force logout. |
| 11 | createdAt | timestamptz | true | |
| 12 | updatedAt | timestamptz | true | |

---

### `refresh_tokens`
Refresh token (rotated). Khi đổi, lưu `replacedByTokenId` để detect replay.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | userId | uuid | true | FK → users(id); cascade. |
| 3 | sessionId | uuid | false | FK → user_sessions(id); SetNull. |
| 4 | tokenHash | varchar(200) | true | UQ. Hash SHA-256 của token gửi cho client. |
| 5 | expiresAt | timestamptz | true | |
| 6 | revokedAt | timestamptz | false | |
| 7 | replacedByTokenId | uuid | false | Phát hiện token cũ bị reuse. |
| 8 | createdAt | timestamptz | true | |
| 9 | updatedAt | timestamptz | true | |

---

### `email_verification_tokens`

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | userId | uuid | true | FK → users(id). |
| 3 | tokenHash | varchar(200) | true | UQ. |
| 4 | email | varchar(200) | true | Email cần verify (cho phép verify khi đổi email). |
| 5 | expiresAt | timestamptz | true | |
| 6 | verifiedAt | timestamptz | false | Khi user click link. |
| 7 | createdAt | timestamptz | true | |
| 8 | updatedAt | timestamptz | true | |

---

### `password_reset_tokens`

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | userId | uuid | true | FK → users(id). |
| 3 | tokenHash | varchar(200) | true | UQ. |
| 4 | expiresAt | timestamptz | true | Thường 15 phút. |
| 5 | usedAt | timestamptz | false | Tránh dùng lại. |
| 6 | ipAddress | varchar(64) | false | IP khi request. |
| 7 | createdAt | timestamptz | true | |
| 8 | updatedAt | timestamptz | true | |

---

### `mfa_factors`
Mỗi user có thể có nhiều factor MFA (TOTP + SMS recovery + Recovery codes…).

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | userId | uuid | true | FK → users(id); cascade. |
| 3 | type | varchar(20) | true | Enum `MfaType`: `Totp/Sms/Email/WebAuthn/RecoveryCode`. |
| 4 | secretEncrypted | text | false | Secret AES-encrypted (TOTP). |
| 5 | phoneNumber | varchar(40) | false | Cho SMS/Voice. |
| 6 | label | varchar(120) | false | "iPhone Authenticator". |
| 7 | isPrimary | boolean | true | Factor mặc định khi user có nhiều. |
| 8 | verifiedAt | timestamptz | false | Sau khi user verify lần đầu. |
| 9 | lastUsedAt | timestamptz | false | |
| 10 | createdAt | timestamptz | true | |
| 11 | updatedAt | timestamptz | true | |

---

### `mfa_challenges`
Mỗi lần đăng nhập tạo 1 challenge tạm; verify code rồi mới issue token.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | factorId | uuid | true | FK → mfa_factors(id); cascade. |
| 3 | codeHash | varchar(200) | true | Hash của OTP gửi cho user. |
| 4 | expiresAt | timestamptz | true | Thường 5 phút. |
| 5 | verifiedAt | timestamptz | false | |
| 6 | attempts | int | true | Đếm số lần nhập sai để rate-limit. |
| 7 | createdAt | timestamptz | true | |
| 8 | updatedAt | timestamptz | true | |

---

### `login_attempts`
Audit mọi lần đăng nhập (success/fail) để chống brute-force.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | email | varchar(200) | true | Email user gõ. IX cùng `createdAt`. |
| 3 | userId | uuid | false | FK → users(id); SetNull. Null nếu email không tồn tại. |
| 4 | result | varchar(30) | true | Enum `LoginResult`: `Success/InvalidCredentials/Locked/MfaRequired/Disabled`. |
| 5 | ipAddress | varchar(64) | false | |
| 6 | userAgent | varchar(500) | false | |
| 7 | failureReason | varchar(200) | false | "wrong-password", "expired", … |
| 8 | createdAt | timestamptz | true | |
| 9 | updatedAt | timestamptz | true | |

---

### `api_keys`
Cho phép gọi API bằng API key thay vì JWT. Key gồm `prefix.secret`; DB chỉ lưu hash.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | true | Org sở hữu key. |
| 3 | createdById | uuid | true | FK → users(id); Restrict. |
| 4 | name | varchar(120) | true | "Zapier integration". |
| 5 | keyPrefix | varchar(20) | true | Phần public hiển thị; IX cùng `orgId`. |
| 6 | keyHash | varchar(200) | true | UQ; hash SHA-256 phần secret. |
| 7 | scopes | jsonb | false | Mảng scope JSON. |
| 8 | expiresAt | timestamptz | false | |
| 9 | lastUsedAt | timestamptz | false | |
| 10 | revokedAt | timestamptz | false | |
| 11 | createdAt | timestamptz | true | |
| 12 | updatedAt | timestamptz | true | |

---

### `invitations`
Mời người vào org. Token unique, hết hạn theo `expiresAt`.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | true | Org được mời vào. IX cùng `email`. |
| 3 | invitedById | uuid | true | FK → users(id); Restrict. |
| 4 | acceptedByUserId | uuid | false | FK → users(id); SetNull. |
| 5 | email | varchar(200) | true | Email người được mời. |
| 6 | token | varchar(200) | true | UQ; gửi qua link email. |
| 7 | roleCode | varchar(60) | false | Role gán mặc định khi accept. |
| 8 | expiresAt | timestamptz | true | |
| 9 | acceptedAt | timestamptz | false | |
| 10 | revokedAt | timestamptz | false | |
| 11 | createdAt | timestamptz | true | |
| 12 | updatedAt | timestamptz | true | |

---

## RBAC

Mô hình: `permission = (function, action[, scope])`. User nhận permission qua `role` (via `role_permissions` + `user_roles`) hoặc override trực tiếp qua `permission_grants` (Allow/Deny).

### `system_functions`
Danh mục chức năng hệ thống (cấp ứng dụng, không phụ thuộc org). Dùng để render RBAC matrix UI.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | code | varchar(80) | true | UQ. Vd `"crm.deals.kanban"`. |
| 3 | name | varchar(120) | true | Tên hiển thị. |
| 4 | description | text | false | |
| 5 | moduleGroup | varchar(30) | true | Enum `SystemModuleGroup`: `Dashboard/Crm/Ai/Library/Reports/Settings/System`. **Tối ưu:** smallint. |
| 6 | path | varchar(200) | false | Path UI `/crm/deals/kanban`. |
| 7 | icon | varchar(60) | false | Tên icon. |
| 8 | riskLevel | varchar(20) | true | Enum `FunctionRiskLevel`: `Low/Medium/High/Critical`. Mặc định `Low`. **Tối ưu:** smallint. |
| 9 | isActive | boolean | true | Mặc định `true`. |
| 10 | order | int | true | Thứ tự hiển thị trong module. |
| 11 | createdAt | timestamptz | true | |
| 12 | updatedAt | timestamptz | true | |

---

### `system_actions`
Danh mục action chuẩn (`view`, `create`, `update`, `delete`, `export`…).

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | code | varchar(40) | true | UQ. |
| 3 | name | varchar(80) | true | |
| 4 | description | text | false | |
| 5 | order | int | true | |
| 6 | createdAt | timestamptz | true | |
| 7 | updatedAt | timestamptz | true | |

---

### `function_actions`
Bảng nối function ↔ action (function nào hỗ trợ action nào).

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | functionId | uuid | true | FK → system_functions(id); cascade. UQ với actionId. |
| 3 | actionId | uuid | true | FK → system_actions(id); cascade. |
| 4 | isDefault | boolean | true | Bật mặc định cho role mới. |

---

### `permissions`
Permission đầy đủ = `(function, action, scope)`.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | functionId | uuid | true | FK → system_functions(id). UQ `(functionId, actionId, scope)`. |
| 3 | actionId | uuid | true | FK → system_actions(id). |
| 4 | scope | varchar(20) | true | Enum `PermissionScope`: `Own/Assigned/Team/Any`. Mặc định `Any`. **Tối ưu:** smallint. |
| 5 | code | varchar(120) | true | UQ; chuỗi rút gọn vd `"crm.deals:view@team"`. |
| 6 | createdAt | timestamptz | true | |
| 7 | updatedAt | timestamptz | true | |

---

### `roles`
Role thuộc về org (`orgId NULL` ⇒ system role dùng chung).

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | false | FK → organizations(id); cascade. UQ `(orgId, code)`. |
| 3 | code | varchar(60) | true | Vd `"admin"`, `"sales"`. |
| 4 | name | varchar(120) | true | |
| 5 | description | text | false | |
| 6 | isSystem | boolean | true | Mặc định `false`. Role mặc định của hệ thống không cho sửa. |
| 7 | parentRoleId | uuid | false | FK → roles(id); SetNull. Hierarchy. |
| 8 | priority | int | true | Cao hơn = quyền lớn hơn. |
| 9 | createdById | uuid | false | FK → users(id); SetNull. |
| 10 | createdAt | timestamptz | true | |
| 11 | updatedAt | timestamptz | true | |

---

### `role_permissions`
Bảng nối role ↔ permission.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | roleId | uuid | true | FK → roles(id); cascade. UQ với `permissionId`. |
| 3 | permissionId | uuid | true | FK → permissions(id); cascade. |

---

### `user_roles`
Bảng nối user ↔ role trong 1 org.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | userId | uuid | true | FK → users(id); cascade. UQ `(userId, orgId, roleId)`. |
| 3 | orgId | uuid | true | |
| 4 | roleId | uuid | true | FK → roles(id); cascade. |
| 5 | assignedById | uuid | false | FK → users(id); SetNull. |
| 6 | expiresAt | timestamptz | false | Cho role tạm thời. |
| 7 | createdAt | timestamptz | true | |
| 8 | updatedAt | timestamptz | true | |

---

### `permission_grants`
Cho phép Allow/Deny permission trực tiếp lên user (override role).

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | userId | uuid | true | FK → users(id); cascade. UQ `(userId, orgId, permissionId)`. |
| 3 | orgId | uuid | true | |
| 4 | permissionId | uuid | true | FK → permissions(id); cascade. |
| 5 | effect | varchar(10) | true | Enum `GrantEffect`: `Allow/Deny`. Mặc định `Allow`. **Tối ưu:** boolean `isDeny`. |
| 6 | reason | text | false | Lý do override. |
| 7 | grantedById | uuid | false | FK → users(id); SetNull. |
| 8 | expiresAt | timestamptz | false | |
| 9 | createdAt | timestamptz | true | |
| 10 | updatedAt | timestamptz | true | |

---

### `teams`
Đội/nhóm trong org. Có thể lồng nhau (`parentTeamId`).

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | true | FK → organizations(id); cascade. UQ `(orgId, code)`. |
| 3 | name | varchar(120) | true | |
| 4 | code | varchar(40) | true | |
| 5 | managerId | uuid | false | FK → users(id); SetNull. |
| 6 | parentTeamId | uuid | false | FK → teams(id); SetNull. |
| 7 | createdAt | timestamptz | true | |
| 8 | updatedAt | timestamptz | true | |

---

### `team_members`

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | teamId | uuid | true | FK → teams(id); cascade. UQ `(teamId, userId)`. |
| 3 | userId | uuid | true | FK → users(id); cascade. |
| 4 | roleType | varchar(20) | true | Enum `TeamRoleType`: `Lead/Member`. **Tối ưu:** boolean `isLead`. |
| 5 | createdAt | timestamptz | true | |
| 6 | updatedAt | timestamptz | true | |

---

## CRM Core

### `companies`
Khách hàng doanh nghiệp. TenantEntity ⇒ có `orgId` + soft delete.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | true | Multi-tenant. IX `(orgId, name)`, `(orgId, taxCode)`. |
| 3 | name | varchar(200) | true | |
| 4 | taxCode | varchar(40) | false | MST. |
| 5 | industry | varchar(80) | false | |
| 6 | size | varchar(40) | false | "1-10", "11-50", … |
| 7 | website | varchar(200) | false | |
| 8 | phone | varchar(40) | false | |
| 9 | email | varchar(200) | false | |
| 10 | address | text | false | |
| 11 | ownerId | uuid | false | FK → users(id); SetNull. Người phụ trách. |
| 12 | customFields | jsonb | false | Field tự định nghĩa theo org. |
| 13 | createdAt | timestamptz | true | |
| 14 | updatedAt | timestamptz | true | |
| 15 | deletedAt | timestamptz | false | Soft delete. |

---

### `contacts`
Người liên hệ (cá nhân) — có thể gắn với 1 company.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | true | IX `(orgId, email)`, `(orgId, phone)`. |
| 3 | companyId | uuid | false | FK → companies(id); SetNull. |
| 4 | fullName | varchar(200) | true | |
| 5 | email | varchar(200) | false | |
| 6 | phone | varchar(40) | false | |
| 7 | title | varchar(120) | false | Chức danh. |
| 8 | isPrimary | boolean | true | Là contact chính của company. |
| 9 | customFields | jsonb | false | |
| 10 | createdAt | timestamptz | true | |
| 11 | updatedAt | timestamptz | true | |
| 12 | deletedAt | timestamptz | false | Soft delete. |

---

### `pipelines`
Quy trình bán hàng. Mỗi org có thể có nhiều pipeline, 1 cái `isDefault=true`.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | true | IX `(orgId, name)`. |
| 3 | name | varchar(120) | true | |
| 4 | isDefault | boolean | true | Mặc định `false`. |
| 5 | order | int | true | Thứ tự hiển thị. |
| 6 | createdAt | timestamptz | true | |
| 7 | updatedAt | timestamptz | true | |
| 8 | deletedAt | timestamptz | false | Soft delete. |

---

### `pipeline_stages`
Stage trong pipeline (Lead → Qualified → Proposal → Won/Lost…).

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | pipelineId | uuid | true | FK → pipelines(id); cascade. UQ `(pipelineId, code)`. |
| 3 | code | varchar(40) | true | "lead", "qualified"… |
| 4 | name | varchar(120) | true | |
| 5 | order | int | true | Thứ tự hiển thị. |
| 6 | defaultProbability | smallint | true | 0–100 (%). Dùng smallint thay int (đủ). |
| 7 | isWon | boolean | true | Stage kết thúc thắng. |
| 8 | isLost | boolean | true | Stage kết thúc thua. |
| 9 | color | varchar(20) | false | Hex màu. |
| 10 | createdAt | timestamptz | true | |
| 11 | updatedAt | timestamptz | true | |

---

### `deals`
Cơ hội bán hàng — entity quan trọng nhất của CRM.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | true | Multi-tenant. IX `(orgId, stageId)`, `(orgId, assigneeId)`. |
| 3 | code | varchar(40) | true | UQ `(orgId, code)`. Vd `"DEAL-000123"`. |
| 4 | title | varchar(200) | true | |
| 5 | pipelineId | uuid | true | FK → pipelines(id); Restrict (chống xoá pipeline còn deal). |
| 6 | stageId | uuid | true | FK → pipeline_stages(id); Restrict. |
| 7 | value | numeric(18,2) | true | Giá trị deal. |
| 8 | currency | varchar(3) | true | ISO 4217. Mặc định `"VND"`. |
| 9 | probability | smallint | true | 0–100 (%). |
| 10 | source | varchar(20) | true | Enum `DealSource`: `Inbound/Outbound/Referral/Marketing/Event/Other`. **Tối ưu:** smallint. |
| 11 | companyId | uuid | false | FK → companies(id); SetNull. |
| 12 | contactId | uuid | false | FK → contacts(id); SetNull. |
| 13 | assigneeId | uuid | false | FK → users(id); SetNull. |
| 14 | expectedCloseDate | date | false | Ngày dự kiến chốt. |
| 15 | actualCloseDate | timestamptz | false | Khi chuyển sang stage Won/Lost. |
| 16 | aiScore | smallint | false | AI score 0–100. |
| 17 | aiScoreReasons | jsonb | false | Mảng lý do model trả về. |
| 18 | lastActivityAt | timestamptz | false | Cập nhật khi có activity mới. |
| 19 | notes | text | false | |
| 20 | customFields | jsonb | false | |
| 21 | createdAt | timestamptz | true | |
| 22 | updatedAt | timestamptz | true | |
| 23 | deletedAt | timestamptz | false | Soft delete. |

---

### `deal_activities`
Hoạt động trên deal (call, email, note, meeting…). Lưu cả thông tin gốc trong `metadata`.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | dealId | uuid | true | FK → deals(id); cascade. IX `(dealId, createdAt)`. |
| 3 | type | varchar(40) | true | `"call"`, `"email"`, `"note"`, `"meeting"`. **Tối ưu:** smallint nếu danh sách cố định. |
| 4 | title | varchar(300) | true | |
| 5 | description | text | false | |
| 6 | metadata | jsonb | false | Payload tự do (email-id, duration…). |
| 7 | actorId | uuid | false | FK → users(id); SetNull. |
| 8 | createdAt | timestamptz | true | |
| 9 | updatedAt | timestamptz | true | |

---

### `deal_stage_history`
Audit di chuyển stage để phân tích funnel + time-to-stage.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | dealId | uuid | true | FK → deals(id); cascade. IX `(dealId, createdAt)`. |
| 3 | fromStageId | uuid | false | FK → pipeline_stages(id); SetNull. Null = mới tạo deal. |
| 4 | toStageId | uuid | true | FK → pipeline_stages(id); Restrict. |
| 5 | durationInPrevStageSeconds | bigint | true | Thời gian ở stage trước (giây). |
| 6 | changedById | uuid | false | FK → users(id); SetNull. |
| 7 | createdAt | timestamptz | true | |
| 8 | updatedAt | timestamptz | true | |

---

### `products`
Catalog sản phẩm/dịch vụ.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | true | UQ `(orgId, code)`. |
| 3 | code | varchar(40) | true | SKU. |
| 4 | name | varchar(200) | true | |
| 5 | description | text | false | |
| 6 | unitPrice | numeric(18,2) | true | Đơn giá. |
| 7 | unit | varchar(20) | true | "pcs", "box", "month". Mặc định `"unit"`. |
| 8 | currency | varchar(3) | true | Mặc định `"VND"`. |
| 9 | isActive | boolean | true | Mặc định `true`. |
| 10 | createdAt | timestamptz | true | |
| 11 | updatedAt | timestamptz | true | |
| 12 | deletedAt | timestamptz | false | Soft delete. |

---

### `deal_products`
Sản phẩm trong deal (line items).

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | dealId | uuid | true | FK → deals(id); cascade. |
| 3 | productId | uuid | true | FK → products(id); Restrict. |
| 4 | quantity | numeric(18,4) | true | Cho phép số lẻ (vd 1.5 tháng). |
| 5 | unitPrice | numeric(18,2) | true | Snapshot giá tại thời điểm thêm. |
| 6 | discountPct | numeric(5,2) | true | 0–100. |
| 7 | total | numeric(18,2) | true | quantity*unitPrice*(1-discount). |
| 8 | createdAt | timestamptz | true | |
| 9 | updatedAt | timestamptz | true | |

---

### `deal_followers`
User follow deal (nhận notification mọi hoạt động). PK composite `(dealId, userId)` (không có `id` riêng).

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | (Có cột id nhưng PK thực là composite — xem cấu hình). |
| 2 | dealId | uuid | true | FK → deals(id); cascade. |
| 3 | userId | uuid | true | FK → users(id); cascade. |
| 4 | followedAt | timestamptz | true | Mặc định `NOW()`. |

---

## Tasks

### `tasks`
Tác vụ độc lập hoặc gắn với deal.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | true | IX `(orgId, assigneeId, status)`, `(orgId, dealId)`. |
| 3 | title | varchar(300) | true | |
| 4 | description | text | false | |
| 5 | assigneeId | uuid | false | FK → users(id); SetNull. |
| 6 | dealId | uuid | false | FK → deals(id); cascade. |
| 7 | dueAt | timestamptz | false | Deadline. |
| 8 | completedAt | timestamptz | false | Thời điểm hoàn thành. |
| 9 | priority | varchar(20) | true | Enum `TaskPriority`: `Low/Medium/High/Urgent`. **Tối ưu:** smallint. |
| 10 | status | varchar(20) | true | Enum `TaskStatus`: `Pending/InProgress/Done/Canceled`. **Tối ưu:** smallint. |
| 11 | createdAt | timestamptz | true | |
| 12 | updatedAt | timestamptz | true | |
| 13 | deletedAt | timestamptz | false | Soft delete. |

---

## Duplicate Detection

### `dup_match_groups`
Nhóm bản ghi nghi trùng (mỗi nhóm = 1 cluster).

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | true | IX `(orgId, entityType, status)`. |
| 3 | entityType | varchar(40) | true | `"Company"`, `"Contact"`. **Tối ưu:** smallint. |
| 4 | matchField | varchar(60) | true | Field dùng để match (vd `"email"`, `"taxCode"`). |
| 5 | confidence | varchar(20) | true | Enum `DupConfidence`: `High/Medium/Low`. **Tối ưu:** smallint. |
| 6 | confidenceScore | numeric(5,4) | true | 0.0000–1.0000. |
| 7 | status | varchar(20) | true | Enum `DupStatus`: `Pending/Resolved/Ignored`. Mặc định `Pending`. **Tối ưu:** smallint. |
| 8 | masterRecordId | uuid | false | Bản ghi gốc giữ lại. |
| 9 | resolvedById | uuid | false | FK → users(id). |
| 10 | resolvedAt | timestamptz | false | |
| 11 | createdAt | timestamptz | true | |
| 12 | updatedAt | timestamptz | true | |
| 13 | deletedAt | timestamptz | false | Soft delete. |

---

### `dup_match_records`
Bản ghi cụ thể trong 1 nhóm trùng.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | matchGroupId | uuid | true | FK → dup_match_groups(id); cascade. |
| 3 | recordId | uuid | true | ID của entity gốc (`companies.id`, `contacts.id`). |
| 4 | recordSnapshot | jsonb | false | Snapshot data tại thời điểm phát hiện. |
| 5 | isMasterCandidate | boolean | true | Đề xuất chọn làm master. |
| 6 | createdAt | timestamptz | true | |
| 7 | updatedAt | timestamptz | true | |

---

## AI

### `ai_insights`
Insight do AI sinh ra (suggest action, cảnh báo risk…).

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | true | IX `(orgId, status, createdAt)`. |
| 3 | scopeType | varchar(40) | true | `"deal"`, `"contact"`, `"company"`. **Tối ưu:** smallint. |
| 4 | scopeId | uuid | true | ID entity insight gắn vào. |
| 5 | type | varchar(60) | true | `"opportunity"`, `"risk"`, `"action"`. |
| 6 | title | varchar(300) | true | |
| 7 | body | text | false | Mô tả dài. |
| 8 | priority | varchar(20) | false | `"low"`, `"medium"`, `"high"`. **Tối ưu:** smallint. |
| 9 | suggestedAction | jsonb | false | Action FE có thể trigger. |
| 10 | model | varchar(80) | false | Tên model (`gpt-4o-mini`). |
| 11 | status | varchar(20) | true | Enum `AiInsightStatus`: `Active/Dismissed/Acted/Expired`. **Tối ưu:** smallint. |
| 12 | expiresAt | timestamptz | false | Auto expire. |
| 13 | dismissedById | uuid | false | FK → users(id). |
| 14 | createdAt | timestamptz | true | |
| 15 | updatedAt | timestamptz | true | |
| 16 | deletedAt | timestamptz | false | Soft delete. |

---

### `ai_score_history`
Lịch sử AI score của deal (mỗi lần model chạy ra 1 row).

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | dealId | uuid | true | FK → deals(id); cascade. IX `(dealId, createdAt)`. |
| 3 | score | smallint | true | 0–100. |
| 4 | reasons | jsonb | false | Lý do model giải thích. |
| 5 | model | varchar(80) | false | |
| 6 | createdAt | timestamptz | true | |
| 7 | updatedAt | timestamptz | true | |

---

## Library & Document Chunks

### `library_nodes`
Cây file/document của org. Root chia 3 loại (Company/Personal/Shared).

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | true | IX `(orgId, rootType, parentId)`. |
| 3 | parentId | uuid | false | FK → library_nodes(id); Restrict. |
| 4 | rootType | varchar(20) | true | Enum `LibraryRootType`: `Company/Personal/Shared`. **Tối ưu:** smallint. |
| 5 | type | varchar(20) | true | Enum `LibraryNodeType`: `Folder/File/Document/Note`. **Tối ưu:** smallint. |
| 6 | name | varchar(300) | true | |
| 7 | status | varchar(20) | true | Enum `LibraryStatus`: `Draft/Active/Archived`. Mặc định `Active`. **Tối ưu:** smallint. |
| 8 | fileId | varchar(120) | false | ID ở storage (S3 key, GDrive id…). |
| 9 | fileUrl | text | false | URL public. |
| 10 | fileMime | varchar(80) | false | `"application/pdf"`. |
| 11 | fileSizeBytes | bigint | false | |
| 12 | path | varchar(2000) | false | Đường dẫn hiển thị. |
| 13 | isSystem | boolean | true | Node hệ thống (không cho xoá). |
| 14 | ownerId | uuid | false | FK → users(id); SetNull. |
| 15 | createdAt | timestamptz | true | |
| 16 | updatedAt | timestamptz | true | |
| 17 | deletedAt | timestamptz | false | Soft delete. |

---

### `library_permissions`
Permission share node cho user/team/role.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | nodeId | uuid | true | FK → library_nodes(id); cascade. UQ `(nodeId, principalType, principalId)`. |
| 3 | principalType | varchar(20) | true | `"user"`, `"team"`, `"role"`. Mặc định `"user"`. **Tối ưu:** smallint. |
| 4 | principalId | uuid | true | ID user/team/role. |
| 5 | permission | varchar(20) | true | `"view"`, `"edit"`, `"manage"`. Mặc định `"view"`. **Tối ưu:** smallint hoặc bitmask. |
| 6 | createdAt | timestamptz | true | |
| 7 | updatedAt | timestamptz | true | |

---

### `document_chunks`
Chunk văn bản đã tách + embedding cho RAG/Chat.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | nodeId | uuid | true | FK → library_nodes(id); cascade. UQ `(nodeId, chunkIndex)`. |
| 3 | orgId | uuid | true | Để filter theo tenant nhanh. |
| 4 | chunkIndex | int | true | Thứ tự chunk trong document. |
| 5 | content | text | true | Nội dung chunk. |
| 6 | contentTokens | int | true | Số token (tính từ tokenizer LLM). |
| 7 | embedding | vector(1536) | false | pgvector, OpenAI text-embedding-3-small. |
| 8 | metadata | jsonb | false | Page number, heading… |
| 9 | createdAt | timestamptz | true | |
| 10 | updatedAt | timestamptz | true | |

> Khuyến nghị tạo index `ivfflat` hoặc `hnsw` trên cột `embedding` để tìm ANN nhanh.

---

## Chat

### `chat_conversations`
Conversation chat với AI (gắn tuỳ chọn với context entity).

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | true | IX `(orgId, userId, lastMessageAt)`. |
| 3 | userId | uuid | true | FK → users(id); cascade. |
| 4 | title | varchar(300) | true | Auto-generate từ tin nhắn đầu. |
| 5 | contextType | varchar(40) | false | `"deal"`, `"contact"`, `"company"`. **Tối ưu:** smallint. |
| 6 | contextId | uuid | false | ID entity context. |
| 7 | pinned | boolean | true | Ghim lên trên. |
| 8 | lastMessageAt | timestamptz | false | Sort recency. |
| 9 | createdAt | timestamptz | true | |
| 10 | updatedAt | timestamptz | true | |
| 11 | deletedAt | timestamptz | false | Soft delete. |

---

### `chat_messages`

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | conversationId | uuid | true | FK → chat_conversations(id); cascade. IX `(conversationId, createdAt)`. |
| 3 | role | varchar(20) | true | Enum `ChatRole`: `User/Assistant/System/Tool`. **Tối ưu:** smallint. |
| 4 | content | text | true | Nội dung message. |
| 5 | contentTokens | int | true | Số token để tính cost. |
| 6 | model | varchar(80) | false | LLM model. |
| 7 | latencyMs | int | false | Thời gian response (ms). |
| 8 | metadata | jsonb | false | Tool-call args, function-call… |
| 9 | createdAt | timestamptz | true | |
| 10 | updatedAt | timestamptz | true | |

---

### `chat_message_sources`
Citation: 1 assistant message tham chiếu đến nhiều `document_chunks`.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | messageId | uuid | true | FK → chat_messages(id); cascade. |
| 3 | chunkId | uuid | true | FK → document_chunks(id); Restrict. |
| 4 | score | numeric(5,4) | true | Cosine similarity 0–1. |
| 5 | label | varchar(200) | false | "Trang 3, File X". |
| 6 | createdAt | timestamptz | true | |
| 7 | updatedAt | timestamptz | true | |

---

## Cross-Cutting

### `notifications`
Thông báo in-app (badge, list dropdown).

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | true | |
| 3 | recipientId | uuid | true | FK → users(id); cascade. IX `(recipientId, readAt)`. |
| 4 | type | varchar(60) | true | `"deal.created"`, `"task.assigned"`. **Tối ưu:** smallint mapping table. |
| 5 | title | varchar(300) | true | |
| 6 | body | text | false | |
| 7 | linkUrl | varchar(500) | false | URL hành động. |
| 8 | entityType | varchar(60) | false | `"Deal"`, `"Task"`. |
| 9 | entityId | uuid | false | |
| 10 | readAt | timestamptz | false | Null = chưa đọc. |
| 11 | createdAt | timestamptz | true | |
| 12 | updatedAt | timestamptz | true | |

---

### `audit_logs`
Audit toàn diện thao tác CUD lên entity quan trọng.

| STT | Tên trường | Kiểu DB | Bắt buộc | Mô tả |
|---|---|---|---|---|
| 1 | id | uuid | true | PK. |
| 2 | orgId | uuid | true | IX `(orgId, entityType, entityId)`, `(orgId, createdAt)`. |
| 3 | actorId | uuid | false | FK → users(id); SetNull. |
| 4 | action | varchar(80) | true | `"Created"`, `"Updated"`, `"Deleted"`. **Tối ưu:** smallint. |
| 5 | entityType | varchar(80) | true | `"Deal"`, `"Contact"`. |
| 6 | entityId | uuid | false | |
| 7 | before | jsonb | false | Snapshot trước. |
| 8 | after | jsonb | false | Snapshot sau. |
| 9 | ipAddress | varchar(64) | false | |
| 10 | userAgent | text | false | |
| 11 | createdAt | timestamptz | true | |
| 12 | updatedAt | timestamptz | true | |

---

## Khuyến nghị tối ưu kiểu dữ liệu

Hiện schema lưu hầu hết enum dạng `varchar(20)` (do EF `HasConversion<string>()`). Đây là lựa chọn dễ đọc, dễ migrate khi thêm giá trị mới, nhưng **không tối ưu cho filter/sort/index trên dataset lớn**. Dưới đây là các phương án bổ sung khi performance trở thành ưu tiên.

### 1. Enum-as-number (tinyint/smallint)

Khi domain enum đã ổn định ít thay đổi → chuyển sang **smallint** (PostgreSQL không có tinyint, dùng `smallint` = 2 byte vẫn rẻ hơn varchar nhiều).

| Trường | Hiện tại | Đề xuất | Ghi chú |
|---|---|---|---|
| `deals.source` | varchar(20) | smallint | Domain enum `DealSource` 6 giá trị, ổn định. |
| `tasks.status` | varchar(20) | smallint | 4 giá trị; filter rất phổ biến trên dashboard. |
| `tasks.priority` | varchar(20) | smallint | 4 giá trị. |
| `ai_insights.status` | varchar(20) | smallint | 4 giá trị. |
| `dup_match_groups.status`, `.confidence` | varchar(20) | smallint | |
| `library_nodes.type`, `.rootType`, `.status` | varchar(20) | smallint | |
| `chat_messages.role` | varchar(20) | smallint | 4 giá trị. |
| `system_functions.moduleGroup`, `.riskLevel` | varchar(20-30) | smallint | |
| `permissions.scope` | varchar(20) | smallint | |
| `team_members.roleType` | varchar(20) | boolean `isLead` | Chỉ 2 giá trị. |
| `permission_grants.effect` | varchar(10) | boolean `isDeny` | Allow/Deny → cờ `isDeny`. |

**Ưu điểm:** lưu 2 byte thay 6–20 byte; index nhỏ hơn nhiều ⇒ filter/sort/aggregate nhanh hơn (đặc biệt `deals` >100k rows).

**Nhược điểm:** join với app code cần lookup table. Khắc phục: giữ tên enum trong code C# + map số ↔ tên ở 1 chỗ duy nhất.

### 2. Bitmask cho flag tập hợp

Nhiều trường flag boolean liên quan có thể gộp thành 1 cột bitmask `int` để kiểm tra nhanh bằng AND/OR. Ví dụ áp dụng:

- **`pipeline_stages`**: `isWon`, `isLost` ⇒ `flags: smallint` với `1 = won`, `2 = lost`. Mở rộng dễ (4 = `isFinal`, 8 = `requiresReason`…).
- **`users`**: `isActive`, `emailVerified` ⇒ `flags: smallint` (`1 = active`, `2 = emailVerified`, `4 = mfaEnabled`, `8 = passwordExpired`…).
- **`library_nodes`**: `isSystem`, có thể thêm `isPinned`, `isStarred`, `isShared` ⇒ `flags: smallint`.
- **`pipelines`**: `isDefault`, có thể thêm `isArchived`, `isPublic`.
- **`api_keys.scopes`**: hiện đang là `jsonb`; nếu danh sách scope cố định, đổi sang bitmask 32 hoặc 64 bit.

**Query mẫu** (đếm deal đang ở stage thắng):
```sql
SELECT count(*) FROM deals d
JOIN pipeline_stages s ON s.id = d.stage_id
WHERE s.flags & 1 = 1;       -- bit 0 = isWon
```

So với cách hiện tại (`s.is_won = true`) thì khi đã có index trên `flags`, AND bitmask cho phép tổ hợp nhiều cờ trong cùng 1 lần quét (`s.flags & 3 != 0` để lấy cả won + lost).

**Tradeoff:**
- Bitmask **mất self-document**: không nhìn thấy ngay flag thứ N nghĩa là gì ⇒ phải tài liệu hoá ở 1 nơi (ví dụ file này) và tạo `SMALLINT` constants ở code.
- Khó index riêng từng cờ; nếu cần filter chỉ trên 1 cờ rất phổ biến, vẫn nên giữ cột boolean riêng + index 1 phần (`partial index`).
- Postgres có sẵn toán tử `&`, `|`, `#`, không cần thêm extension.

### 3. Các tối ưu phụ trợ

| Hạng mục | Khuyến nghị |
|---|---|
| `tasks.priority/status` | Tạo partial index để dashboard filter "task của tôi chưa done": `CREATE INDEX ix_my_open_tasks ON tasks (assignee_id) WHERE status IN (0,1) AND deleted_at IS NULL;` |
| `deals.value` | Đã `numeric(18,2)` — phù hợp tiền tệ; tránh đổi sang float để không sai số. |
| `currency` | `varchar(3)` (ISO 4217) đủ — không cần optimize. |
| Soft delete | Đảm bảo mọi bảng có `deleted_at` đều có partial index `WHERE deleted_at IS NULL` để query thường không quét row đã xoá. |
| `audit_logs.before/after` | Khi DB lớn, cân nhắc partition theo tháng (`PARTITION BY RANGE (created_at)`) hoặc đẩy sang bảng cold storage / S3. |
| `document_chunks.embedding` | Tạo `CREATE INDEX ... USING hnsw (embedding vector_cosine_ops)` khi vượt vài chục nghìn chunk. |
| `notifications.recipientId, readAt` | Đã có IX; có thể đổi sang partial index `WHERE read_at IS NULL` để truy vấn badge "thông báo mới" rất rẻ. |
| `user_sessions.expiresAt` | Tạo job clean định kỳ + index `WHERE revoked_at IS NULL` để chỉ giữ active sessions. |
| `email`, `phone`, `taxCode` | Có thể thêm `pg_trgm` GIN index `gin_trgm_ops` để hỗ trợ `ILIKE '%abc%'` nhanh trên `companies.name`, `contacts.fullName`. |

### 4. Lộ trình chuyển đổi không downtime

Để đổi cột varchar enum → smallint mà không gây gián đoạn:

1. Thêm cột mới `<col>_v2: smallint` nullable, viết trigger/EF interceptor đồng bộ giá trị mới ↔ cũ.
2. Backfill dữ liệu cũ bằng query: `UPDATE deals SET source_v2 = CASE source WHEN 'Inbound' THEN 0 ... END`.
3. Cập nhật code chỉ đọc cột mới, vẫn ghi cả 2.
4. Sau khi ổn định, drop cột varchar, rename `_v2` về tên cũ.

Quy trình này áp dụng được cho mọi enum đề xuất ở mục 1.

---

## Phụ lục — Mapping Enum ↔ Smallint đề xuất

Để giữ ổn định khi chuyển sang số, dùng đúng thứ tự dưới đây (không thay đổi sau khi đã release):

| Enum | Giá trị | Mã |
|---|---|---|
| `DealSource` | Inbound / Outbound / Referral / Marketing / Event / Other | 0 / 1 / 2 / 3 / 4 / 5 |
| `TaskStatus` | Pending / InProgress / Done / Canceled | 0 / 1 / 2 / 3 |
| `TaskPriority` | Low / Medium / High / Urgent | 0 / 1 / 2 / 3 |
| `DupConfidence` | High / Medium / Low | 0 / 1 / 2 |
| `DupStatus` | Pending / Resolved / Ignored | 0 / 1 / 2 |
| `AiInsightStatus` | Active / Dismissed / Acted / Expired | 0 / 1 / 2 / 3 |
| `LibraryNodeType` | Folder / File / Document / Note | 0 / 1 / 2 / 3 |
| `LibraryStatus` | Draft / Active / Archived | 0 / 1 / 2 |
| `LibraryRootType` | Company / Personal / Shared | 0 / 1 / 2 |
| `ChatRole` | User / Assistant / System / Tool | 0 / 1 / 2 / 3 |
| `AuthProvider` | Password / Google / Microsoft / Apple / Saml / Oidc | 0 / 1 / 2 / 3 / 4 / 5 |
| `MfaType` | Totp / Sms / Email / WebAuthn / RecoveryCode | 0 / 1 / 2 / 3 / 4 |
| `LoginResult` | Success / InvalidCredentials / Locked / MfaRequired / Disabled | 0 / 1 / 2 / 3 / 4 |
| `GrantEffect` | Allow / Deny | 0 / 1 |
| `PermissionScope` | Own / Assigned / Team / Any | 0 / 1 / 2 / 3 |
| `TeamRoleType` | Lead / Member | 0 / 1 |
| `SystemModuleGroup` | Dashboard / Crm / Ai / Library / Reports / Settings / System | 0 / 1 / 2 / 3 / 4 / 5 / 6 |
| `FunctionRiskLevel` | Low / Medium / High / Critical | 0 / 1 / 2 / 3 |
