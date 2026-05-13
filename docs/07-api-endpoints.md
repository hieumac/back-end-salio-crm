# 07 — API endpoints (v1)

> Toàn bộ endpoint hiện có ở v1. Base URL: `http://localhost:5080/api/v1`.

## Quy ước chung

### URL convention

```
/api/v{version}/{module}/{resource}[/{id}][/{sub-action}]
```

- `version`: số nguyên hoặc `major.minor` (`1`, `1.0`, `2`).
- `module`: nhóm chức năng (`crm`, `auth`, `ai`, `library`, `system`...).
- `resource`: plural snake-or-kebab tùy module (`deals`, `companies`, `system/functions`).
- `id`: GUID.
- `sub-action`: verb cho action không CRUD (`/stage`, `/move`, `/restore`).

Ví dụ:
- `GET /api/v1/crm/deals` — list
- `GET /api/v1/crm/deals/{id}` — detail
- `POST /api/v1/crm/deals` — create
- `PATCH /api/v1/crm/deals/{id}/stage` — đổi stage

### HTTP methods

| Method | Mục đích | Idempotent |
|---|---|---|
| `GET` | Đọc resource | ✅ |
| `POST` | Tạo mới hoặc action không idempotent | ❌ |
| `PUT` | Thay thế toàn bộ resource | ✅ |
| `PATCH` | Update một phần | ❌ (thường) |
| `DELETE` | Xóa | ✅ |

### Status codes

| Code | Khi nào |
|---|---|
| 200 | OK với body |
| 201 | Created — POST tạo mới (header `Location`) |
| 204 | No Content — DELETE thành công |
| 400 | DomainException khác |
| 401 | Chưa authenticate (thiếu/sai JWT) |
| 403 | ForbiddenException — không có permission |
| 404 | NotFoundException |
| 409 | ConflictException |
| 422 | ValidationException (FluentValidation) |
| 500 | Internal — exception không lường |

### Headers

| Header | Required | Mô tả |
|---|---|---|
| `Authorization: Bearer <jwt>` | Có (trừ endpoint anonymous) | Access token |
| `Content-Type: application/json` | POST/PUT/PATCH | Body JSON |
| `Accept: application/json` | Optional | Mặc định JSON |
| `X-Api-Version: 1.0` | Optional | Override version (thay vì URL segment) |

### Pagination query params

```
?page=1&pageSize=20&sort=updatedAt&desc=true&q=keyword
```

| Param | Default | Note |
|---|---|---|
| `page` | 1 | 1-based |
| `pageSize` | 20 | max 100 |
| `sort` | `updatedAt` | tên field |
| `desc` | true | sort direction |
| `q` | — | search string (ILike) |

### Response shape

```json
// 200 OK
{
  "success": true,
  "data": { ... },
  "message": null
}

// 200 OK với list
{
  "success": true,
  "data": {
    "items": [...],
    "page": 1,
    "pageSize": 20,
    "total": 123,
    "totalPages": 7
  }
}

// 4xx/5xx
{
  "success": false,
  "error": {
    "code": "VALIDATION",
    "message": "Validation failed",
    "details": [{ "field": "title", "message": "Required" }]
  },
  "traceId": "00-..."
}
```

## Auth module

### `POST /api/v1/auth/register`

> Tạo organization + user owner đầu tiên. Anonymous.

Request:
```json
{
  "email": "owner@example.com",
  "password": "Sup3rSecret!",
  "fullName": "Owner Tên",
  "organizationName": "Acme Corp",
  "organizationSlug": "acme"
}
```

Response 200:
```json
{
  "success": true,
  "data": {
    "accessToken": "eyJ...",
    "refreshToken": "abc...",
    "accessExpiresAt": "2026-05-13T10:00:00Z",
    "refreshExpiresAt": "2026-06-12T10:00:00Z",
    "user": { "id": "...", "email": "...", "fullName": "..." },
    "organization": { "id": "...", "slug": "acme" }
  }
}
```

Lỗi:
- 409 `CONFLICT` — email đã tồn tại / slug đã dùng.
- 422 `VALIDATION` — password yếu, email sai format.

### `POST /api/v1/auth/login`

> Đăng nhập email/password. Anonymous.

Request:
```json
{ "email": "owner@example.com", "password": "Sup3rSecret!" }
```

Response 200: giống `register`.

Lỗi:
- 401 — sai password (log `LoginAttempt`).
- 403 `FORBIDDEN` — account bị disable.

### `POST /api/v1/auth/refresh`

> Refresh token rotation. Anonymous.

Request:
```json
{ "refreshToken": "abc..." }
```

Response 200: token mới (access + refresh). Token cũ **bị revoke** ngay.

Lỗi:
- 401 — token không tồn tại / đã revoke / hết hạn.

## CRM — Deals

Route base: `/api/v1/crm/deals`. Yêu cầu `[Authorize]`.

### `GET /api/v1/crm/deals`

> Permission: `crm.deals.list:view`

Query:
```
?page=1&pageSize=20&q=acme&stageId={guid}&assigneeId={guid}&pipelineId={guid}&sort=value&desc=true
```

Response 200:
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": "...",
        "code": "DEAL-202605-00001",
        "title": "Acme — Q2 license",
        "value": 50000,
        "currency": "USD",
        "stageId": "...",
        "stageName": "Qualified",
        "companyName": "Acme Corp",
        "aiScore": 78,
        "expectedCloseDate": "2026-06-30"
      }
    ],
    "page": 1,
    "pageSize": 20,
    "total": 42,
    "totalPages": 3
  }
}
```

### `GET /api/v1/crm/deals/{id}`

> Permission: `crm.deals.detail:view`

Response 200: `DealDto` đầy đủ (Description, CustomFields, AiScoreReasons, ContactId, AssigneeId...).

Lỗi 404 nếu deal không thuộc org user.

### `POST /api/v1/crm/deals`

> Permission: `crm.deals.list:create`

Request:
```json
{
  "title": "Acme — Q3 expansion",
  "value": 75000,
  "currency": "USD",
  "pipelineId": "{guid}",
  "stageId": "{guid}",
  "companyId": "{guid?}",
  "contactId": "{guid?}",
  "assigneeId": "{guid?}",
  "source": "Inbound",
  "expectedCloseDate": "2026-09-30",
  "description": "...",
  "customFields": { "leadSource": "webinar" }
}
```

Response 200:
```json
{ "success": true, "data": "{deal-guid}" }
```

Behavior:
- Auto-gen `code` = `DEAL-YYYYMM-NNNNN` (sequence theo tháng/org).
- Set `Probability` từ `PipelineStage.DefaultProbability` nếu không gửi.
- Insert `DealActivity` type=`deal_created` với actor = current user.

Lỗi:
- 422 — title rỗng, value < 0, pipeline/stage không hợp lệ.
- 404 — pipeline/stage/company/contact không tồn tại trong org.

### `PATCH /api/v1/crm/deals/{id}/stage`

> Permission: `crm.deals.detail:update`

Request:
```json
{ "stageId": "{guid}", "note": "Customer confirmed budget" }
```

Response 200: empty `data`.

Behavior:
- Insert `DealStageHistory` với `DurationInPrevStageSeconds`.
- Insert `DealActivity` type=`stage_changed`.
- Nếu stage mới `IsWon` hoặc `IsLost` → set `ActualCloseDate = today`.

## System (RBAC catalog)

Route base: `/api/v1/system`. Authorize required.

### `GET /api/v1/system/functions`

> Permission: `system.permissions:view`

Trả về 30 function chuẩn (cho UI RBAC matrix).

```json
{
  "success": true,
  "data": [
    { "id": "...", "code": "crm.deals.list", "name": "Danh sách deals", "moduleGroup": "Crm", "riskLevel": "Low" }
  ]
}
```

### `GET /api/v1/system/actions`

> Permission: `system.permissions:view`

Trả về 15 action chuẩn:

```json
{
  "success": true,
  "data": [
    { "id": "...", "code": "view", "name": "Xem" },
    { "id": "...", "code": "create", "name": "Tạo" }
  ]
}
```

## Health

### `GET /api/v1/health`

> Anonymous. Liveness probe.

Response 200:
```json
{ "success": true, "data": { "status": "ok", "timestamp": "..." } }
```

## CRM — Companies

Route base: `/api/v1/crm/companies`. Authorize required.

| Method | Path | Permission | Mô tả |
|---|---|---|---|
| GET | `/` | `crm.companies:view` | List + search + filter (industry, ownerId) |
| GET | `/{id}` | `crm.companies:view` | Chi tiết + count deal/contact |
| POST | `/` | `crm.companies:create` | Tạo company |
| PUT | `/{id}` | `crm.companies:update` | Update company |
| DELETE | `/{id}` | `crm.companies:delete` | Soft delete (block nếu còn deal active → 409) |

## CRM — Contacts

Route base: `/api/v1/crm/contacts`. Authorize required.

| Method | Path | Permission | Mô tả |
|---|---|---|---|
| GET | `/` | `crm.contacts:view` | List + search + filter companyId |
| GET | `/{id}` | `crm.contacts:view` | Chi tiết |
| POST | `/` | `crm.contacts:create` | Tạo contact, auto-demote IsPrimary cũ trong cùng company |
| PUT | `/{id}` | `crm.contacts:update` | Update |
| DELETE | `/{id}` | `crm.contacts:delete` | Soft delete |

## CRM — Pipelines

Route base: `/api/v1/crm/pipelines`. Authorize required.

| Method | Path | Permission | Mô tả |
|---|---|---|---|
| GET | `/` | `crm.pipelines:view` | List tất cả pipeline + nested stages (kèm DealCount) |
| POST | `/` | `crm.pipelines:create` | Tạo pipeline + stages (atomic); IsDefault=true sẽ hạ pipeline default cũ |

## CRM — Products

Route base: `/api/v1/crm/products`. Authorize required.

| Method | Path | Permission | Mô tả |
|---|---|---|---|
| GET | `/` | `crm.products:view` | List + search |
| POST | `/` | `crm.products:create` | Tạo (Code unique trong org → 409 nếu trùng) |
| PUT | `/{id}` | `crm.products:update` | Update |

## Tasks

Route base: `/api/v1/tasks`. Authorize required.

| Method | Path | Permission | Mô tả |
|---|---|---|---|
| GET | `/` | `crm.tasks:view` | List + filter (assigneeId, dealId, status, mineOnly) |
| POST | `/` | `crm.tasks:create` | Tạo task, default assignee = current user |
| PUT | `/{id}` | `crm.tasks:update` | Update + transition status (auto set CompletedAt) |
| POST | `/{id}/complete` | `crm.tasks:update` | Shortcut đánh dấu Done (idempotent) |
| DELETE | `/{id}` | `crm.tasks:delete` | Soft delete |

## Users

Route base: `/api/v1/users`. Authorize required.

| Method | Path | Permission | Mô tả |
|---|---|---|---|
| GET | `/me` | (chỉ cần login) | Profile + org hiện tại + roles |
| GET | `/` | `settings.users:view` | List user thuộc org (join qua OrgMember) |

## Organizations

Route base: `/api/v1/organizations`. Authorize required.

| Method | Path | Permission | Mô tả |
|---|---|---|---|
| GET | `/current` | (chỉ cần login) | Org đang truy cập + member count |
| PUT | `/current` | `settings.organization:update` | Update tên/locale |

## Endpoint roadmap (chưa làm)

Các endpoint sau đang chờ implement:

| Module | Endpoints dự kiến |
|---|---|
| **Users** | `POST /api/v1/users/invite`, `PATCH /{id}/roles`, `PATCH /{id}/activate` |
| **Organizations** | `GET /members`, `GET/POST /invitations` |
| **Duplicates** | `GET/POST /api/v1/crm/duplicates`, `/merge` |
| **AI Insights** | `GET /api/v1/ai/insights`, `POST /api/v1/ai/score/{dealId}` |
| **Library** | `GET/POST/DELETE /api/v1/library/nodes`, `/search` (vector) |
| **Chat** | `GET/POST /api/v1/chat/conversations`, `/messages` (stream) |
| **Reports** | `GET /api/v1/reports/sales`, `/pipeline`, `/forecast` |
| **Notifications** | `GET /api/v1/notifications`, `PATCH /{id}/read` |
| **Audit log** | `GET /api/v1/system/audit-logs` |
| **Teams** | CRUD `/api/v1/rbac/teams`, `/members` |
| **Roles** | CRUD `/api/v1/rbac/roles`, `/permissions` |

Mỗi endpoint khi thêm phải:
1. Đặt route đúng convention.
2. Gắn `[RequirePermission(fnCode, actCode)]`.
3. Định nghĩa Command/Query trong Application.
4. Định nghĩa Validator.
5. Wrap response bằng `ApiResponse<T>`.
