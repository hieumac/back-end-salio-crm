# 09 — Authentication

> Email/password + JWT bearer + refresh token rotation. Multi-provider sẵn schema (Google/Microsoft) nhưng chưa implement.

## Tổng quan flow

```
Frontend           Backend                       DB
   │                  │                            │
   │  POST /register  │                            │
   ├─────────────────►│                            │
   │                  │ Create Org + User          │
   │                  │ Create AuthIdentity (Pass) ├─────►
   │                  │ Hash password (BCrypt)     │
   │                  │ Create OrgMember           │
   │                  │ Assign role=owner          │
   │                  │ Create UserSession         │
   │                  │ Issue JWT + Refresh        │
   │  { access, refresh, user, org }               │
   │◄─────────────────│                            │
   │                  │                            │
   │  GET /me (Bearer)│                            │
   ├─────────────────►│ Validate JWT               │
   │                  │ Load claims                │
   │  { user }        │                            │
   │◄─────────────────│                            │
   │                  │                            │
   │  POST /refresh   │                            │
   ├─────────────────►│ Hash incoming token        │
   │                  │ Lookup RefreshToken row    ├─────►
   │                  │ Revoke old + issue new     │
   │                  │ Link via ReplacedByTokenId │
   │  { access, refresh }                          │
   │◄─────────────────│                            │
```

## Register

`POST /api/v1/auth/register` — anonymous.

Handler `RegisterCommandHandler`:

1. Check email không tồn tại (`Users.Any(u => u.Email == cmd.Email)`) → throw `ConflictException("Email already exists")`.
2. Check slug không trùng → throw `ConflictException("Organization slug taken")`.
3. Tạo `Organization { Slug, Name, Plan="free" }`.
4. Tạo `User { Email, FullName, EmailVerified=false }`.
5. Tạo `AuthIdentity { UserId, Provider=Password, PasswordHash=BCrypt.Hash(password) }`.
6. Tạo `OrgMember { OrgId, UserId, IsActive=true, JoinedAt=now }`.
7. Resolve `Role { Code="owner", IsSystem=true }` → tạo `UserRole { UserId, OrgId, RoleId }`.
8. Tạo `UserSession` + `RefreshToken` + sign JWT.
9. SaveChanges.

Response: token + user + org info.

### Password policy (FluentValidation)

```csharp
RuleFor(x => x.Password)
    .MinimumLength(8)
    .Matches("[A-Z]").WithMessage("Cần ít nhất 1 chữ hoa")
    .Matches("[0-9]").WithMessage("Cần ít nhất 1 số")
    .Matches("[^a-zA-Z0-9]").WithMessage("Cần ít nhất 1 ký tự đặc biệt");
```

## Login

`POST /api/v1/auth/login` — anonymous.

Handler `LoginCommandHandler`:

1. Lookup user by email — không có → log `LoginAttempt(Result=InvalidCredentials)` + throw `ForbiddenException`.
2. Check `User.IsActive` — false → log `Result=Disabled` + throw `ForbiddenException`.
3. Load `AuthIdentity` provider=Password — verify `BCrypt.Verify(input, hash)`. Sai → log + throw.
4. Load roles từ `UserRole` (chưa expire) join `Role`.
5. Tạo `UserSession { UserId, OrgId, IpAddress, UserAgent, ExpiresAt=now+30d }`.
6. Sign JWT với claims `sub, email, jti, org_id, role (multi)`.
7. Sinh refresh token (256-bit random base64-url), hash SHA-256, lưu `RefreshToken { TokenHash, SessionId, ExpiresAt }`.
8. Log `LoginAttempt(Result=Success)`.
9. Update `User.LastLoginAt`.
10. SaveChanges.

### Anti brute force

`LoginAttempt` mọi lần (kể cả fail). Có thể thêm rate limiting middleware:

```csharp
services.AddRateLimiter(o => o.AddFixedWindowLimiter("login", opt =>
{
    opt.PermitLimit = 5;
    opt.Window = TimeSpan.FromMinutes(1);
}));

[EnableRateLimiting("login")]
public async Task<IActionResult> Login(...) { ... }
```

(Chưa setup, gợi ý mở rộng.)

## Refresh token rotation

`POST /api/v1/auth/refresh` — anonymous.

Handler `RefreshTokenCommandHandler`:

1. Hash incoming token `SHA256(raw)`.
2. Lookup `RefreshToken { TokenHash, RevokedAt=null, ExpiresAt > now }`.
3. Không có hoặc đã revoke → throw `ForbiddenException` (token reuse — security event).
4. Revoke token cũ: `oldToken.RevokedAt = now`.
5. Tạo token mới + link: `oldToken.ReplacedByTokenId = newToken.Id`.
6. Issue JWT mới với claims giống session cũ.
7. SaveChanges → trả token mới.

### Reuse detection

Nếu một refresh token đã revoke được dùng lại → có thể là token bị leak. Khuyến nghị extend:

```csharp
if (oldToken.RevokedAt != null)
{
    // Revoke toàn bộ session — buộc user login lại
    session.RevokedAt = now;
    // Hoặc revoke cả chain ReplacedByTokenId
}
```

## JWT cấu trúc

Access token:

```json
// header
{ "alg": "HS256", "typ": "JWT" }

// payload
{
  "sub": "user-guid",
  "email": "user@example.com",
  "jti": "session-guid",
  "org_id": "org-guid",
  "role": ["owner", "admin"],
  "iss": "salio.api",
  "aud": "salio.frontend",
  "exp": 1747142400,
  "iat": 1747140600
}

// signature
HMACSHA256(base64Url(header) + "." + base64Url(payload), JwtOptions.Secret)
```

### `JwtOptions`

```json
"Jwt": {
  "Secret": "REPLACE_WITH_AT_LEAST_64_CHAR_RANDOM_BYTES_BASE64",
  "Issuer": "salio.api",
  "Audience": "salio.frontend",
  "AccessTokenLifetimeMinutes": 30,
  "RefreshTokenLifetimeDays": 30
}
```

> **Secret bắt buộc đổi ở prod.** Tối thiểu 64 bytes random (256+ bits). Lưu trong env var hoặc secret manager.

### Validation params

```csharp
opt.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,
    ValidIssuer = jwt.Issuer,
    ValidateAudience = true,
    ValidAudience = jwt.Audience,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
    ClockSkew = TimeSpan.FromMinutes(1),
    RoleClaimType = ClaimTypes.Role,
    NameClaimType = ClaimTypes.Email
};
```

## Logout (chưa implement)

Endpoint `POST /api/v1/auth/logout` gợi ý:

1. Lấy `sessionId` từ JWT (`jti`).
2. Revoke `UserSession` + tất cả `RefreshToken` của session đó.
3. Trả 204.

Note: JWT vẫn valid đến khi expire (stateless) — nếu cần invalidate tức thì phải dùng blacklist (Redis) hoặc rút ngắn `AccessTokenLifetimeMinutes`.

## Email verification (chưa implement)

Schema có `EmailVerificationToken { UserId, TokenHash, ExpiresAt, ConsumedAt }`.

Flow gợi ý:
1. Register → tạo token random, hash, lưu DB → gửi email link `https://app/verify?token=raw`.
2. User click → endpoint verify hash, set `User.EmailVerified = true`, set `ConsumedAt = now`.

## Password reset (chưa implement)

Schema `PasswordResetToken` tương tự. Flow:
1. `POST /auth/forgot-password { email }` → tạo token, gửi email.
2. `POST /auth/reset-password { token, newPassword }` → verify token, update `AuthIdentity.PasswordHash`, revoke toàn bộ session active.

## MFA (chưa implement)

Schema sẵn:
- `MfaFactor { UserId, Type (Totp/Sms/Email/WebAuthn/RecoveryCode), Secret, IsEnabled }`
- `MfaChallenge { FactorId, Code, ExpiresAt, Attempts }`

Flow gợi ý:
1. Login với password đúng → check `MfaFactor.IsEnabled` cho user.
2. Có MFA → trả `{ requiresMfa: true, challengeId }` thay vì token.
3. `POST /auth/mfa/verify { challengeId, code }` → verify, trả token.

## OAuth (Google/Microsoft) — chưa implement

Schema `AuthIdentity` support đa provider qua `Provider` enum. Setup:

```csharp
builder.Services.AddAuthentication()
    .AddGoogle(opt => { opt.ClientId = ...; opt.ClientSecret = ...; })
    .AddMicrosoftAccount(...);
```

Callback: lookup `AuthIdentity` theo `(Provider, ProviderUserId)` — match → login, không match → tạo mới (nếu policy cho phép) hoặc gắn với user hiện tại.

## Bảo mật khuyến nghị

- ✅ HTTPS bắt buộc ở prod (`app.UseHttpsRedirection()` + HSTS).
- ✅ Password BCrypt workFactor ≥ 12.
- ✅ Refresh token lưu hash (không lưu plain).
- ✅ JWT secret tối thiểu 256 bits, đổi định kỳ.
- ✅ CORS strict — chỉ allow origins thật.
- ⚠️ Rate limit `/auth/*` (chưa setup).
- ⚠️ CSRF không cần với JWT thuần (không dùng cookie), nhưng nếu chuyển sang cookie HttpOnly thì phải thêm token CSRF.
- ⚠️ Audit log mọi sự kiện auth quan trọng (login, logout, password change, role grant) — bảng `AuditLog` đã có sẵn.
