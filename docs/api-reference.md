# Salio CRM — API Reference

> Tổng hợp toàn bộ REST API của Salio Backend (`Salio.Api`). Tất cả endpoint nằm dưới prefix `/api/v{version}/...`. Hiện tại version duy nhất là **v1**.

## Quy ước chung

### Response wrapper — Template chuẩn cho mọi endpoint

Mọi API (cả thành công lẫn lỗi) đều trả về **cùng một cấu trúc**:

```json
{
  "status": "success",
  "code": 200,
  "message": "Lấy thông tin người dùng thành công",
  "data": { "id": 12345, "username": "hoangnv", "email": "...", "role": "admin" }
}
```

| Trường | Kiểu | Có khi | Mô tả |
|---|---|---|---|
| `status` | string | luôn có | `"success"` khi thành công, `"error"` khi lỗi. |
| `code` | int | luôn có | HTTP status code (200, 201, 400, 401, 404, 422, 500…). |
| `message` | string? | tùy chọn | Mô tả ngắn dùng cho toast / log. Có thể null. |
| `data` | T? | thành công | Payload (object / list / primitive). `null` khi lỗi hoặc endpoint không trả về dữ liệu. |
| `errors` | object? | khi lỗi | Chi tiết lỗi: `{ "code": "VALIDATION", "details": [...] }`. Ẩn khi không có. |
| `traceId` | string? | khi lỗi | Trace ID phục vụ debug / đối soát log. Ẩn khi không có. |

**Ví dụ — Thành công không có data (UPDATE / DELETE / action):**
```json
{ "status": "success", "code": 200, "message": "Updated" }
```

**Ví dụ — Tạo mới (201):**
```json
{ "status": "success", "code": 201, "data": "9b9c1a4e-..." }
```

**Ví dụ — Validation lỗi (422):**
```json
{
  "status": "error",
  "code": 422,
  "message": "Validation failed",
  "errors": {
    "code": "VALIDATION",
    "details": [
      { "field": "Email", "error": "Email không hợp lệ" },
      { "field": "Password", "error": "Tối thiểu 8 ký tự" }
    ]
  },
  "traceId": "0HMV2C9R5XQ3F:00000001"
}
```

**Ví dụ — Domain error (404 / 403 / 409):**
```json
{
  "status": "error",
  "code": 404,
  "message": "Không tìm thấy bản ghi",
  "errors": { "code": "NOT_FOUND" },
  "traceId": "0HMV..."
}
```

> **Cách hoạt động (server side):** `ResponseWrapperFilter` (đăng ký global trong `Program.cs`) tự bọc mọi response chưa wrap. `ExceptionHandlingMiddleware` đảm bảo các exception (`ValidationException`, `NotFoundException`, `ForbiddenException`, `ConflictException`, `DomainException`, …) đều xuất ra cùng format này.

### Phân trang (`PagedResult<T>`)
```json
{
  "items": [...],
  "total": 123,
  "page": 1,
  "pageSize": 20
}
```

### Authentication
- Hầu hết endpoint yêu cầu JWT Bearer token: `Authorization: Bearer <accessToken>`.
- Token lấy được từ `POST /api/v1/auth/login` hoặc `/refresh`.
- Endpoint `[AllowAnonymous]` (đăng nhập / đăng ký / health) không cần token.

### Authorization
- Backend dùng RBAC theo `(functionCode, actionCode)` (xem `system_functions`, `system_actions`).
- Bảng dưới đây cột "Permission" ghi rõ quyền cần có để gọi được endpoint.

### Versioning
- URL: `/api/v1/...`
- Header thay thế: `X-Api-Version: 1.0`

---

## Mục lục

| Nhóm | Prefix | Mô tả |
|---|---|---|
| [Auth](#1-auth) | `/api/v1/auth` | Đăng nhập, đăng ký, refresh token |
| [Health](#2-health) | `/api/v1/health` | Healthcheck |
| [Users](#3-users) | `/api/v1/users` | Hồ sơ user, list user trong org |
| [Organizations](#4-organizations) | `/api/v1/organizations` | Thông tin org hiện tại |
| [Companies](#5-companies) | `/api/v1/crm/companies` | Khách hàng doanh nghiệp |
| [Contacts](#6-contacts) | `/api/v1/crm/contacts` | Người liên hệ |
| [Pipelines](#7-pipelines) | `/api/v1/crm/pipelines` | Pipeline + Stage |
| [Deals](#8-deals) | `/api/v1/crm/deals` | Deal / Cơ hội bán hàng |
| [Products](#9-products) | `/api/v1/crm/products` | Sản phẩm |
| [Tasks](#10-tasks) | `/api/v1/tasks` | Tác vụ |
| [System Functions](#11-system-functions) | `/api/v1/system/functions` | RBAC matrix |

---

## 1. Auth

`/api/v1/auth` — Public, không cần token. Controller: `AuthController`.

### 1.1 POST `/login`
Đăng nhập bằng email + password.

| Mục | Giá trị |
|---|---|
| Method | `POST` |
| Auth | Public |
| Permission | – |

**Request body (`LoginCommand`)**

| Field | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `email` | string | true | Email user. |
| `password` | string | true | Mật khẩu thô (sẽ được kiểm tra bằng BCrypt). |
| `orgSlug` | string? | false | Slug org muốn đăng nhập (khi user thuộc nhiều org). Mặc định: org gần nhất. |

**Response 200 (`ApiResponse<LoginResponse>`)**

| Field | Kiểu | Mô tả |
|---|---|---|
| `accessToken` | string | JWT, dùng cho header `Authorization: Bearer …`. |
| `refreshToken` | string | Token dùng để gọi `/refresh`. |
| `accessExpiresAt` | DateTimeOffset | Thời điểm access token hết hạn. |
| `userId` | Guid | ID user. |
| `orgId` | Guid | ID organization đang đăng nhập. |
| `email` | string | Email user. |
| `fullName` | string | Tên hiển thị. |
| `roles` | string[] | Danh sách mã role trong org hiện tại. |

---

### 1.2 POST `/register`
Đăng ký user mới đồng thời tạo organization riêng cho user đó (chế độ self-signup, user trở thành Owner).

| Mục | Giá trị |
|---|---|
| Method | `POST` |
| Auth | Public |
| Permission | – |

**Request body (`RegisterCommand`)**

| Field | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `email` | string | true | Email; phải unique trong toàn hệ thống. |
| `password` | string | true | Mật khẩu thô (sẽ hash BCrypt). |
| `fullName` | string | true | Tên hiển thị. |
| `orgName` | string | true | Tên tổ chức mới sẽ được tạo (slug sinh tự động). |

**Response 201 (`ApiResponse<Guid>`)** — `data` là `userId` mới tạo.

---

### 1.3 POST `/refresh`
Lấy access token mới bằng refresh token.

| Mục | Giá trị |
|---|---|
| Method | `POST` |
| Auth | Public |
| Permission | – |

**Request body (`RefreshTokenCommand`)**

| Field | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `refreshToken` | string | true | Refresh token (giá trị raw, đối chiếu với hash trong DB). |

**Response 200 (`ApiResponse<LoginResponse>`)** — cấu trúc giống `/login`.

---

## 2. Health

### 2.1 GET `/api/v1/health`
Kiểm tra service sống.

| Mục | Giá trị |
|---|---|
| Method | `GET` |
| Auth | Public |

**Response 200**

```json
{
  "status": "success",
  "code": 200,
  "data": { "status": "ok", "version": "1.0", "time": "2026-05-18T15:30:00Z" }
}
```

---

## 3. Users

`/api/v1/users` — Cần Bearer.

### 3.1 GET `/me`
Lấy thông tin user đang đăng nhập + org hiện tại + roles.

| Mục | Giá trị |
|---|---|
| Method | `GET` |
| Auth | Bearer |
| Permission | – (chỉ cần login) |

**Response 200 (`ApiResponse<UserMeDto>`)**

| Field | Kiểu | Mô tả |
|---|---|---|
| `id` | Guid | ID user. |
| `email` | string | Email. |
| `fullName` | string | Tên hiển thị. |
| `avatarUrl` | string? | URL avatar (nullable). |
| `emailVerified` | bool | Đã verify email chưa. |
| `currentOrgId` | Guid? | ID org hiện tại. |
| `currentOrgName` | string? | Tên org hiện tại. |
| `roles` | string[] | Mã role trong org hiện tại. |
| `lastLoginAt` | DateTimeOffset? | Lần đăng nhập gần nhất. |

---

### 3.2 GET `/`
Liệt kê user trong org hiện tại.

| Mục | Giá trị |
|---|---|
| Method | `GET` |
| Auth | Bearer |
| Permission | `settings.users:view` |

**Query (`ListUsersQuery`)**

| Field | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `page` | int | 1 | Trang hiện tại (>=1). |
| `pageSize` | int | 20 | Số bản ghi 1 trang. |
| `search` | string? | – | Tìm kiếm trên email/fullName. |
| `isActive` | bool? | – | Lọc user active/inactive. |

**Response 200 (`ApiResponse<PagedResult<UserListItemDto>>`)**

`UserListItemDto`:

| Field | Kiểu | Mô tả |
|---|---|---|
| `id` | Guid | |
| `email` | string | |
| `fullName` | string | |
| `avatarUrl` | string? | |
| `isActive` | bool | |
| `roles` | string[] | |
| `lastLoginAt` | DateTimeOffset? | |

---

## 4. Organizations

`/api/v1/organizations` — Cần Bearer.

### 4.1 GET `/current`
Lấy thông tin org hiện tại của user.

| Mục | Giá trị |
|---|---|
| Method | `GET` |
| Auth | Bearer |
| Permission | – |

**Response 200 (`ApiResponse<OrganizationDto>`)**

| Field | Kiểu | Mô tả |
|---|---|---|
| `id` | Guid | |
| `slug` | string | Slug định danh. |
| `name` | string | Tên hiển thị. |
| `plan` | string? | Plan thanh toán (`free`, `pro`…). |
| `locale` | string? | Ngôn ngữ mặc định (`vi-VN`). |
| `memberCount` | int | Số thành viên active. |
| `createdAt` | DateTimeOffset | |

---

### 4.2 PUT `/current`
Cập nhật thông tin org hiện tại.

| Mục | Giá trị |
|---|---|
| Method | `PUT` |
| Auth | Bearer |
| Permission | `settings.organization:update` |

**Request body (`UpdateOrganizationCommand`)**

| Field | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `name` | string | true | Tên hiển thị mới. |
| `locale` | string? | false | Locale mới. |

**Response 200 (`ApiResponse`)** — `data` null, `message: "Updated"`.

---

## 5. Companies

`/api/v1/crm/companies` — Cần Bearer.

### 5.1 GET `/`
Danh sách company có filter + phân trang.

| Mục | Giá trị |
|---|---|
| Permission | `crm.companies:view` |

**Query (`ListCompaniesQuery`)**

| Field | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `page` | int | 1 | |
| `pageSize` | int | 20 | |
| `search` | string? | – | Tìm trên tên/email/phone. |
| `industry` | string? | – | Lọc theo ngành. |
| `ownerId` | Guid? | – | Lọc theo người phụ trách. |
| `sortBy` | string? | `"updatedAt"` | Tên field sort. |
| `sortDir` | string? | `"desc"` | `"asc"` hoặc `"desc"`. |

**Response 200 (`ApiResponse<PagedResult<CompanyListItemDto>>`)**

`CompanyListItemDto`:

| Field | Kiểu | Mô tả |
|---|---|---|
| `id` | Guid | |
| `name` | string | |
| `industry` | string? | |
| `email` | string? | |
| `phone` | string? | |
| `ownerName` | string? | |
| `dealCount` | int | Số deal đang gắn với company. |
| `updatedAt` | DateTimeOffset | |

---

### 5.2 GET `/{id}`
Chi tiết company.

| Permission | `crm.companies:view` |

**Path**: `id: Guid`

**Response 200 (`ApiResponse<CompanyDto>`)**

`CompanyDto`: `id`, `name`, `taxCode?`, `industry?`, `size?`, `website?`, `phone?`, `email?`, `address?`, `ownerId?`, `ownerName?`, `dealCount`, `contactCount`, `createdAt`, `updatedAt`.

---

### 5.3 POST `/`
Tạo company mới. **201 Created**, trả `Guid` mới.

| Permission | `crm.companies:create` |

**Request body (`CreateCompanyCommand`)**

| Field | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `name` | string | true | Tên công ty. |
| `taxCode` | string? | false | Mã số thuế (nên unique trong org). |
| `industry` | string? | false | |
| `size` | string? | false | (vd `1-10`, `11-50`). |
| `website` | string? | false | |
| `phone` | string? | false | |
| `email` | string? | false | |
| `address` | string? | false | |
| `ownerId` | Guid? | false | User được giao phụ trách. |

---

### 5.4 PUT `/{id}`
Cập nhật company. Yêu cầu `id` trên URL **trùng** `id` trong body, nếu không trả 400.

| Permission | `crm.companies:update` |

**Body (`UpdateCompanyCommand`)** — `id` + các field như `CreateCompanyCommand`.

---

### 5.5 DELETE `/{id}`
Xoá mềm company.

| Permission | `crm.companies:delete` |

---

## 6. Contacts

`/api/v1/crm/contacts` — Cần Bearer.

### 6.1 GET `/`

| Permission | `crm.contacts:view` |

**Query (`ListContactsQuery`)**: `page`, `pageSize`, `search?`, `companyId?`, `sortBy?` (`updatedAt`), `sortDir?` (`desc`).

**Response item (`ContactListItemDto`)**: `id`, `fullName`, `email?`, `phone?`, `title?`, `companyId?`, `companyName?`, `isPrimary`.

---

### 6.2 GET `/{id}`

| Permission | `crm.contacts:view` |

**Response (`ContactDto`)**: `id`, `companyId?`, `companyName?`, `fullName`, `email?`, `phone?`, `title?`, `isPrimary`, `createdAt`, `updatedAt`.

---

### 6.3 POST `/`

| Permission | `crm.contacts:create` |

**Body (`CreateContactCommand`)**

| Field | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `fullName` | string | true | |
| `companyId` | Guid? | false | Gắn với company. |
| `email` | string? | false | |
| `phone` | string? | false | |
| `title` | string? | false | Chức vụ. |
| `isPrimary` | bool | false (default `false`) | Đánh dấu là contact chính của company. |

---

### 6.4 PUT `/{id}`, DELETE `/{id}`

Tương tự Companies (`crm.contacts:update`, `crm.contacts:delete`).

---

## 7. Pipelines

`/api/v1/crm/pipelines` — Cần Bearer.

### 7.1 GET `/`
Liệt kê toàn bộ pipeline + stage trong org.

| Permission | `crm.pipelines:view` |

**Response (`ApiResponse<PipelineDto[]>`)**

`PipelineDto`: `id`, `name`, `isDefault`, `order`, `stages: PipelineStageDto[]`.

`PipelineStageDto`: `id`, `code`, `name`, `order`, `defaultProbability` (int 0–100), `isWon`, `isLost`, `color?`, `dealCount`.

---

### 7.2 POST `/`
Tạo pipeline + danh sách stage trong cùng 1 lần.

| Permission | `crm.pipelines:create` |

**Body (`CreatePipelineCommand`)**

| Field | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `name` | string | true | |
| `isDefault` | bool | true | Đánh dấu pipeline mặc định (chỉ 1 cái default/org). |
| `stages` | `StageInput[]` | true | Danh sách stage. |

`StageInput`: `code`, `name`, `order`, `defaultProbability`, `isWon`, `isLost`, `color?`.

---

## 8. Deals

`/api/v1/crm/deals` — Cần Bearer.

### 8.1 GET `/`

| Permission | `crm.deals.list:view` |

**Query (`ListDealsQuery`)**: `page`, `pageSize`, `search?`, `pipelineId?`, `stageId?`, `assigneeId?`, `sortBy?` (`createdAt`), `sortDir?` (`desc`).

**Item (`DealListItemDto`)**: `id`, `code`, `title`, `value`, `currency`, `stageId`, `stageName?`, `companyName?`, `aiScore?`, `expectedCloseDate?`.

---

### 8.2 GET `/{id}`

| Permission | `crm.deals.detail:view` |

**Response (`DealDto`)**: `id`, `code`, `title`, `value` (decimal), `currency`, `probability` (int 0–100), `pipelineId`, `stageId`, `stageName?`, `companyId?`, `companyName?`, `contactId?`, `contactName?`, `assigneeId?`, `assigneeName?`, `expectedCloseDate?` (DateOnly), `aiScore?` (int 0–100), `lastActivityAt?`, `createdAt`.

---

### 8.3 POST `/`
Tạo deal mới.

| Permission | `crm.deals.list:create` |

**Body (`CreateDealCommand`)**

| Field | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `title` | string | true | |
| `pipelineId` | Guid | true | |
| `stageId` | Guid | true | Stage khởi tạo (thường là stage đầu). |
| `value` | decimal | true | Giá trị deal. |
| `currency` | string | true | `"VND"`, `"USD"`… |
| `source` | enum `DealSource` | true | `Inbound`, `Outbound`, `Referral`, `Marketing`, `Event`, `Other`. |
| `companyId` | Guid? | false | |
| `contactId` | Guid? | false | |
| `assigneeId` | Guid? | false | User được giao. |
| `expectedCloseDate` | DateOnly? | false | |
| `notes` | string? | false | |

---

### 8.4 PATCH `/{id}/stage`
Di chuyển deal sang stage khác (drag-drop trên Kanban). Tự động ghi `deal_stage_history`.

| Permission | `crm.deals.detail:update` |

**Body**

| Field | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `toStageId` | Guid | true | Stage đích. |
| `note` | string? | false | Ghi chú di chuyển. |

---

## 9. Products

`/api/v1/crm/products` — Cần Bearer.

### 9.1 GET `/`

| Permission | `crm.products:view` |

**Query (`ListProductsQuery`)**: `page`, `pageSize`, `search?`, `isActive?`.

**Item (`ProductListItemDto`)**: `id`, `code`, `name`, `unitPrice`, `unit`, `currency`, `isActive`.

---

### 9.2 POST `/`

| Permission | `crm.products:create` |

**Body (`CreateProductCommand`)**

| Field | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `code` | string | true | Unique trong org. |
| `name` | string | true | |
| `description` | string? | false | |
| `unitPrice` | decimal | true | |
| `unit` | string | false (default `"unit"`) | (`pcs`, `box`, `month`…) |
| `currency` | string | false (default `"VND"`) | |
| `isActive` | bool | false (default `true`) | |

---

### 9.3 PUT `/{id}`

| Permission | `crm.products:update` |

**Body (`UpdateProductCommand`)**: `id`, `name`, `description?`, `unitPrice`, `unit`, `currency`, `isActive`.

---

## 10. Tasks

`/api/v1/tasks` — Cần Bearer.

### 10.1 GET `/`

| Permission | `crm.tasks:view` |

**Query (`ListTasksQuery`)**

| Field | Kiểu | Mặc định | Mô tả |
|---|---|---|---|
| `page` | int | 1 | |
| `pageSize` | int | 20 | |
| `search` | string? | – | Tìm trên title. |
| `assigneeId` | Guid? | – | Lọc theo người được giao. |
| `dealId` | Guid? | – | Lọc theo deal. |
| `status` | enum `TaskStatus`? | – | `Pending` / `InProgress` / `Done` / `Canceled`. |
| `mineOnly` | bool? | – | Chỉ task của tôi (assignee = current user). |

**Item (`TaskListItemDto`)**: `id`, `title`, `assigneeId?`, `assigneeName?`, `dealId?`, `dueAt?`, `priority` (`Low/Medium/High/Urgent`), `status`.

---

### 10.2 POST `/`

| Permission | `crm.tasks:create` |

**Body (`CreateTaskCommand`)**

| Field | Kiểu | Bắt buộc | Mô tả |
|---|---|---|---|
| `title` | string | true | |
| `description` | string? | false | |
| `assigneeId` | Guid? | false | |
| `dealId` | Guid? | false | Gắn với deal. |
| `dueAt` | DateTimeOffset? | false | |
| `priority` | enum `TaskPriority` | false (default `Medium`) | `Low/Medium/High/Urgent`. |

---

### 10.3 PUT `/{id}`

| Permission | `crm.tasks:update` |

**Body (`UpdateTaskCommand`)**: `id`, `title`, `description?`, `assigneeId?`, `dealId?`, `dueAt?`, `priority`, `status`.

---

### 10.4 POST `/{id}/complete`
Đánh dấu task hoàn thành (set `status=Done`, `completedAt=now`).

| Permission | `crm.tasks:update` |

---

### 10.5 DELETE `/{id}`

| Permission | `crm.tasks:delete` |

---

## 11. System Functions

`/api/v1/system/functions` — Cần Bearer.

### 11.1 GET `/`
List toàn bộ function + actions được phép, dùng để render RBAC matrix.

| Permission | `system.functions:view` |

**Response (`ApiResponse<FunctionDto[]>`)**

`FunctionDto`: `id`, `code`, `name`, `moduleGroup` (`Dashboard/Crm/Ai/Library/Reports/Settings/System`), `riskLevel` (`Low/Medium/High/Critical`), `path?`, `actions: ActionDto[]`.

`ActionDto`: `id`, `code`, `name`.

---

### 11.2 GET `/api/v1/system/actions`
List action chuẩn của hệ thống (`view`, `create`, `update`, `delete`, `export`…).

| Permission | `system.functions:view` |

---

## 12. Phụ lục — Enum string values trả về

Một số enum hiện được serialize dưới dạng **string** (do `JsonStringEnumConverter` cấu hình trong `Program.cs`). FE nên match theo tên:

| Enum | Giá trị |
|---|---|
| `DealSource` | `Inbound`, `Outbound`, `Referral`, `Marketing`, `Event`, `Other` |
| `TaskStatus` | `Pending`, `InProgress`, `Done`, `Canceled` |
| `TaskPriority` | `Low`, `Medium`, `High`, `Urgent` |
| `SystemModuleGroup` | `Dashboard`, `Crm`, `Ai`, `Library`, `Reports`, `Settings`, `System` |
| `FunctionRiskLevel` | `Low`, `Medium`, `High`, `Critical` |
