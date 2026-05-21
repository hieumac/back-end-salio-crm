# 12 — Deployment

> Hướng dẫn deploy production lên **Windows Server 2022 + IIS**. PostgreSQL cài trực tiếp trên VPS, không dùng Docker.

## Checklist trước khi go-live

### Code & config

- [ ] **JWT Secret**: random ≥ 64 bytes, lưu IIS Environment Variable (KHÔNG commit vào source).
- [ ] **DB password**: random mạnh, lưu IIS Environment Variable.
- [ ] **CORS origins**: chỉ allow domain frontend thật, không `localhost`.
- [ ] **HTTPS**: bắt buộc — cấu hình SSL Binding trong IIS.
- [ ] **HSTS**: bật `app.UseHsts()` cho production.
- [ ] **AutoMigrate**: `false` — chạy migration thủ công trước khi deploy.
- [ ] **AutoSeed**: `false` sau lần seed đầu.
- [ ] **Swagger**: `false` trên production.
- [ ] **Include Error Detail**: xóa khỏi connection string production.
- [ ] **Logging level**: `Information` cho app, `Warning` cho Microsoft.*.

### Hạ tầng

- [ ] PostgreSQL 16 cài trên VPS, service tự khởi động cùng Windows.
- [ ] Database backup tự động (Task Scheduler + pg_dump), giữ 30 ngày.
- [ ] Windows Event Log hoặc file log `.\\logs\\salio-*.log` trong thư mục deploy.
- [ ] Health check endpoint `/api/v1/health` hoạt động sau deploy.
- [ ] Rate limiting cho `/auth/*` (hoặc cấu hình IIS IP/Domain Restriction).
- [ ] Windows Firewall: chỉ mở port 80, 443 ra ngoài; port 5432 chỉ localhost.

### Security

- [ ] App Pool chạy với Identity riêng (không dùng `ApplicationPoolIdentity` mặc định cho prod).
- [ ] Thư mục deploy: chỉ App Pool Identity có quyền Write vào `logs\`.
- [ ] Connection string không có `Include Error Detail=true`.
- [ ] Cập nhật .NET Hosting Bundle định kỳ (security patches).
- [ ] `dotnet list package --vulnerable` định kỳ.

---

## Chuẩn bị Windows Server 2022

### 1. Cài IIS

Mở PowerShell (Admin):

```powershell
# Bật IIS + các feature cần thiết
Enable-WindowsOptionalFeature -Online -FeatureName `
    IIS-WebServerRole, IIS-WebServer, IIS-CommonHttpFeatures, `
    IIS-HttpErrors, IIS-HttpLogging, IIS-RequestFiltering, `
    IIS-StaticContent, IIS-DefaultDocument, IIS-DirectoryBrowsing, `
    IIS-ASPNET45, IIS-ISAPIExtensions, IIS-ISAPIFilter, `
    IIS-HttpCompressionStatic, IIS-HttpCompressionDynamic, `
    IIS-ManagementConsole -All
```

### 2. Cài .NET 10 Hosting Bundle

Tải tại: https://dotnet.microsoft.com/download/dotnet/10.0
→ Chọn: **ASP.NET Core Runtime 10.x — Windows Hosting Bundle**

```powershell
# Sau khi cài xong, reset IIS để load module mới
iisreset
```

Kiểm tra:
```powershell
dotnet --list-runtimes
# Phải thấy: Microsoft.AspNetCore.App 10.x.x
```

### 3. Cài PostgreSQL 16

Tải tại: https://www.postgresql.org/download/windows/
→ Installer (EDB): chọn PostgreSQL Server + Command Line Tools.

Trong quá trình cài:
- Port: `5432`
- Superuser password: mật khẩu mạnh (lưu riêng)
- Locale: `Vietnamese, Vietnam` hoặc `English, United States`

Sau khi cài, tạo user + database cho Salio:

```sql
-- Chạy trong psql hoặc pgAdmin
CREATE USER salio WITH PASSWORD 'STRONG_PASSWORD_HERE';
CREATE DATABASE salio OWNER salio ENCODING 'UTF8';
GRANT ALL PRIVILEGES ON DATABASE salio TO salio;

-- Bật extension pgvector (bắt buộc cho AI features)
\c salio
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pg_trgm;
```

Bật pgvector — tải pre-built binary tại https://github.com/pgvector/pgvector/releases
hoặc dùng `pgvector` installer tương thích với phiên bản PostgreSQL 16 trên Windows.

Cấu hình PostgreSQL chỉ lắng nghe localhost (mặc định đã là vậy):
```ini
# C:\Program Files\PostgreSQL\16\data\postgresql.conf
listen_addresses = 'localhost'
```

---

## Publish ứng dụng

### Build & Publish từ máy dev

```powershell
# Publish framework-dependent (server phải có .NET 10 Hosting Bundle)
dotnet publish src\Salio.Api\Salio.Api.csproj `
    -c Release `
    -r win-x64 `
    --no-self-contained `
    -o .\publish

# Hoặc publish self-contained (không cần cài .NET trên server)
dotnet publish src\Salio.Api\Salio.Api.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o .\publish
```

Copy thư mục `publish\` lên server (dùng SCP, SFTP, hoặc Robocopy):

```powershell
# Ví dụ copy qua network share
robocopy .\publish \\VPS-SERVER\C$\inetpub\wwwroot\salio-api /MIR /XD logs
```

Thư mục deploy gợi ý: `C:\inetpub\wwwroot\salio-api\`

---

## Cấu hình IIS

### 1. Tạo Application Pool

Mở **IIS Manager** → Application Pools → Add Application Pool:

| Thuộc tính | Giá trị |
|---|---|
| Name | `SalioApiPool` |
| .NET CLR Version | **No Managed Code** (ASP.NET Core tự quản lý runtime) |
| Managed Pipeline Mode | Integrated |
| Start automatically | True |

### 2. Tạo Website / Application

- Sites → Add Website (hoặc thêm Application vào Default Web Site)
- **Physical path**: `C:\inetpub\wwwroot\salio-api`
- **Application Pool**: `SalioApiPool`
- **Binding**: HTTPS, port 443, hostname `api.yourdomain.com`

### 3. Cấu hình Environment Variables trong IIS

**Cách A — qua IIS Manager (khuyến nghị):**

IIS Manager → Sites → SalioApi → Configuration Editor
→ `system.webServer/aspNetCore` → `environmentVariables` → thêm:

| Name | Value |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__Default` | `Host=localhost;Port=5432;Database=salio;Username=salio;Password=STRONG_PASSWORD` |
| `Jwt__Secret` | `64-char-random-secret` |
| `Cors__Origins__0` | `https://yourfrontend.com` |

**Cách B — chỉnh trực tiếp `web.config`** (không commit secret vào git):

```xml
<environmentVariables>
  <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
  <environmentVariable name="ConnectionStrings__Default"
    value="Host=localhost;Port=5432;Database=salio;Username=salio;Password=STRONG_PASSWORD" />
  <environmentVariable name="Jwt__Secret"
    value="YOUR_64_CHAR_SECRET_HERE" />
</environmentVariables>
```

> ⚠️ **Không commit `web.config` có chứa secret lên git.** Dùng `.gitignore` hoặc quản lý file này ngoài source control trên server.

### 4. Phân quyền thư mục

```powershell
# Cho phép App Pool Identity đọc file deploy
icacls "C:\inetpub\wwwroot\salio-api" /grant "IIS AppPool\SalioApiPool:(OI)(CI)RX"

# Cho phép ghi vào thư mục logs
New-Item -ItemType Directory -Force "C:\inetpub\wwwroot\salio-api\logs"
icacls "C:\inetpub\wwwroot\salio-api\logs" /grant "IIS AppPool\SalioApiPool:(OI)(CI)M"
```

### 5. SSL Certificate

```powershell
# Dùng Win-ACME (Let's Encrypt miễn phí cho IIS)
# Tải: https://www.win-acme.com/

wacs.exe --target iis --siteid <site-id> --installation iis
```

Hoặc import certificate mua sẵn qua IIS Manager → Server Certificates → Import.

---

## Chạy Database Migration

> **KHÔNG bật `AutoMigrate: true` trên production.** Chạy migration thủ công trước khi deploy.

```powershell
# Từ máy dev — generate SQL script idempotent
dotnet ef migrations script `
    --project src\Salio.Infrastructure `
    --startup-project src\Salio.Api `
    --idempotent `
    --output .\migrations\v1.0.0.sql

# Apply lên server production
psql -h localhost -U salio -d salio -f .\migrations\v1.0.0.sql
```

Hoặc dùng EF Bundle (chạy trực tiếp trên server):

```powershell
# Build bundle trên máy dev
dotnet ef migrations bundle `
    --project src\Salio.Infrastructure `
    --startup-project src\Salio.Api `
    --runtime win-x64 `
    -o .\efbundle.exe

# Copy efbundle.exe lên server và chạy
.\efbundle.exe --connection "Host=localhost;Port=5432;Database=salio;Username=salio;Password=STRONG_PASSWORD"
```

---

## Quy trình deploy cập nhật (Update)

```powershell
# 1. Publish bản mới ra thư mục tạm
dotnet publish src\Salio.Api\Salio.Api.csproj -c Release -r win-x64 --no-self-contained -o .\publish-new

# 2. Chạy migration nếu có thay đổi schema
psql -h localhost -U salio -d salio -f .\migrations\vX.Y.Z.sql

# 3. Dừng App Pool (tránh file lock)
Invoke-Command -ComputerName VPS-SERVER -ScriptBlock {
    Import-Module WebAdministration
    Stop-WebAppPool -Name "SalioApiPool"
}

# 4. Copy file mới lên (giữ nguyên thư mục logs)
robocopy .\publish-new \\VPS-SERVER\C$\inetpub\wwwroot\salio-api /MIR /XD logs

# 5. Khởi động lại App Pool
Invoke-Command -ComputerName VPS-SERVER -ScriptBlock {
    Start-WebAppPool -Name "SalioApiPool"
}

# 6. Smoke test
curl https://api.yourdomain.com/api/v1/health
```

---

## Backup PostgreSQL (Task Scheduler)

Tạo script `C:\Scripts\backup-salio.ps1`:

```powershell
$date = Get-Date -Format "yyyyMMdd"
$backupDir = "C:\Backups\salio"
$backupFile = "$backupDir\salio-$date.backup"

New-Item -ItemType Directory -Force $backupDir | Out-Null

& "C:\Program Files\PostgreSQL\16\bin\pg_dump.exe" `
    -h localhost -U salio -d salio `
    -F c -f $backupFile

# Xóa backup cũ hơn 30 ngày
Get-ChildItem $backupDir -Filter "*.backup" |
    Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } |
    Remove-Item -Force

Write-Host "Backup completed: $backupFile"
```

Đăng ký Task Scheduler chạy 2:00 AM hàng ngày:

```powershell
$action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-File C:\Scripts\backup-salio.ps1"
$trigger = New-ScheduledTaskTrigger -Daily -At "02:00"
$settings = New-ScheduledTaskSettingsSet -RunOnlyIfNetworkAvailable
Register-ScheduledTask -TaskName "SalioDbBackup" -Action $action -Trigger $trigger -Settings $settings -RunLevel Highest
```

---

## Kiểm tra sau deploy

```powershell
# Health check
curl https://api.yourdomain.com/api/v1/health

# Kiểm tra log gần nhất
Get-Content "C:\inetpub\wwwroot\salio-api\logs\salio-$(Get-Date -Format 'yyyyMMdd').log" -Tail 50

# Kiểm tra App Pool đang chạy
Get-WebAppPoolState -Name "SalioApiPool"

# Kiểm tra PostgreSQL service
Get-Service -Name "postgresql-x64-16"
```

---

## Rollback

```powershell
# Giữ 3 bản publish gần nhất ở C:\Deployments\salio\
# Rollback = dừng pool → copy bản cũ → khởi động pool

Stop-WebAppPool -Name "SalioApiPool"
robocopy "C:\Deployments\salio\v1.0.1" "C:\inetpub\wwwroot\salio-api" /MIR /XD logs
Start-WebAppPool -Name "SalioApiPool"
```

> DB migration rollback: ưu tiên forward-only migration. Nếu phải rollback schema → chạy SQL thủ công đã chuẩn bị trước.
