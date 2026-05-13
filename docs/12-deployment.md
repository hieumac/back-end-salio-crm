# 12 — Deployment

> Checklist + hướng dẫn deploy production. Hai tùy chọn: VM Linux (Docker) hoặc managed services (Azure App Service / AWS ECS).

## Checklist trước khi go-live

### Code & config

- [ ] **JWT Secret**: random ≥ 64 bytes, lưu env var / secret manager (KHÔNG commit).
- [ ] **DB password**: random mạnh, lưu env var.
- [ ] **CORS origins**: chỉ allow domain frontend thật, không `localhost`.
- [ ] **HTTPS**: bắt buộc — terminate ở reverse proxy hoặc app.
- [ ] **HSTS**: bật `app.UseHsts()` cho production.
- [ ] **AutoMigrate**: tắt — chạy migration qua CI/CD.
- [ ] **AutoSeed**: tắt sau lần seed đầu.
- [ ] **Swagger**: tắt hoặc đưa sau gateway có auth.
- [ ] **EnableSensitiveDataLogging**: tắt — không log password/token.
- [ ] **Logging level**: `Information` cho app, `Warning` cho Microsoft.*.

### Hạ tầng

- [ ] Database backup tự động (daily), test restore.
- [ ] Monitor / alerting (CPU, memory, disk, error rate).
- [ ] Centralized log (ELK, Seq, Datadog, hoặc CloudWatch).
- [ ] Health check endpoint cấu hình trong load balancer (`/api/v1/health`).
- [ ] Rate limiting cho `/auth/*` và public endpoint.
- [ ] WAF (CloudFlare, AWS WAF) cho IP throttle, SQL injection rules.

### Security

- [ ] Connection string không có `Include Error Detail=true` ở prod.
- [ ] Cập nhật `.NET runtime` định kỳ (security patches).
- [ ] `npm audit` / `dotnet list package --vulnerable` định kỳ.
- [ ] Audit log enable (đã có sẵn `AuditLog` entity).
- [ ] MFA cho admin accounts (chưa implement — TODO).
- [ ] Secret rotation policy (DB password, JWT secret, OAuth client secret).

## Cấu hình production

### `appsettings.Production.json`

```json
{
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
  },
  "Database": {
    "AutoMigrate": false,
    "AutoSeed": false
  },
  "Cors": {
    "Origins": [ "https://app.salio.com" ]
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": { "Microsoft": "Warning", "System": "Warning" }
    },
    "WriteTo": [
      { "Name": "Console" },
      { "Name": "File", "Args": { "path": "/var/log/salio/salio-.log", "rollingInterval": "Day", "retainedFileCountLimit": 30 } }
    ]
  }
}
```

### Env vars (override appsettings)

Nested key dùng `__` (double underscore) ở Linux, `:` ở Windows.

```bash
export ConnectionStrings__Default="Host=db.prod;Port=5432;Database=salio;Username=salio;Password=$(cat /run/secrets/db_pwd)"
export Jwt__Secret="$(cat /run/secrets/jwt_secret)"
export Jwt__Issuer="salio.api"
export Jwt__Audience="salio.frontend"
export ASPNETCORE_ENVIRONMENT="Production"
export ASPNETCORE_URLS="http://0.0.0.0:8080"
```

## Option A: Deploy Docker (VM Linux)

### Dockerfile

`backend-dotnet/Dockerfile`:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Salio.Domain/*.csproj src/Salio.Domain/
COPY src/Salio.Application/*.csproj src/Salio.Application/
COPY src/Salio.Infrastructure/*.csproj src/Salio.Infrastructure/
COPY src/Salio.Api/*.csproj src/Salio.Api/
RUN dotnet restore src/Salio.Api/Salio.Api.csproj

COPY . .
RUN dotnet publish src/Salio.Api/Salio.Api.csproj -c Release -o /app /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Salio.Api.dll"]
```

Build + push:

```bash
docker build -t salio-api:1.0.0 .
docker tag salio-api:1.0.0 your-registry/salio-api:1.0.0
docker push your-registry/salio-api:1.0.0
```

### `docker-compose.prod.yml`

```yaml
services:
  postgres:
    image: pgvector/pgvector:pg16
    restart: always
    environment:
      POSTGRES_DB: salio
      POSTGRES_USER: salio
      POSTGRES_PASSWORD_FILE: /run/secrets/db_password
    volumes:
      - pgdata:/var/lib/postgresql/data
    secrets:
      - db_password
    networks: [salio-net]
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U salio"]
      interval: 10s
      timeout: 5s
      retries: 5

  api:
    image: your-registry/salio-api:1.0.0
    restart: always
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__Default: "Host=postgres;Port=5432;Database=salio;Username=salio;Password_FILE=/run/secrets/db_password"
      Jwt__Secret_FILE: /run/secrets/jwt_secret
    secrets:
      - db_password
      - jwt_secret
    depends_on:
      postgres: { condition: service_healthy }
    networks: [salio-net]
    ports:
      - "8080:8080"

  caddy:
    image: caddy:2
    restart: always
    ports: ["80:80", "443:443"]
    volumes:
      - ./Caddyfile:/etc/caddy/Caddyfile
      - caddy-data:/data
    networks: [salio-net]

secrets:
  db_password: { file: ./secrets/db_password.txt }
  jwt_secret: { file: ./secrets/jwt_secret.txt }

volumes:
  pgdata:
  caddy-data:

networks:
  salio-net:
```

### Caddyfile (auto-HTTPS)

```
api.salio.com {
    reverse_proxy api:8080
    encode gzip
    log {
        output file /var/log/caddy/access.log
    }
}
```

Caddy tự xin Let's Encrypt cert qua HTTP-01 challenge.

### Deploy

```bash
ssh prod-server
cd /opt/salio
git pull
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
docker compose -f docker-compose.prod.yml logs -f api
```

### Migration on deploy

Không bật AutoMigrate ở prod. Chạy migration thủ công:

```bash
# Sinh script (lúc dev)
dotnet ef migrations script <FromMigration> <ToMigration> \
  --project src/Salio.Infrastructure --startup-project src/Salio.Api \
  --output ./migrations/v1.0.1.sql --idempotent

# Apply trên prod
docker exec -i salio-postgres psql -U salio -d salio < migrations/v1.0.1.sql
```

Hoặc EF bundle:

```bash
dotnet ef migrations bundle --project src/Salio.Infrastructure --startup-project src/Salio.Api -o ./efbundle
./efbundle --connection "Host=db.prod;..."
```

## Option B: Azure App Service

```bash
# Tạo resource group, plan, app
az group create -n salio-rg -l southeastasia
az appservice plan create -n salio-plan -g salio-rg --is-linux --sku B2
az webapp create -n salio-api -g salio-rg -p salio-plan -r "DOTNETCORE:9.0"

# DB
az postgres flexible-server create -n salio-db -g salio-rg --version 16 \
  --admin-user salio --admin-password "<strong>" --sku-name Standard_B1ms

# Bật pgvector
az postgres flexible-server parameter set --resource-group salio-rg --server-name salio-db \
  --name azure.extensions --value vector,uuid-ossp,pg_trgm

# App settings
az webapp config appsettings set -n salio-api -g salio-rg --settings \
  ConnectionStrings__Default="..." \
  Jwt__Secret="..." \
  ASPNETCORE_ENVIRONMENT="Production"

# Deploy (zip)
dotnet publish src/Salio.Api -c Release -o ./publish
cd publish && zip -r ../publish.zip . && cd ..
az webapp deploy -n salio-api -g salio-rg --src-path publish.zip --type zip
```

## Option C: AWS ECS Fargate

Khung sơ bộ:
- Push Docker image lên ECR.
- Task definition với env vars từ Parameter Store / Secrets Manager.
- ALB → ECS service.
- RDS PostgreSQL với pgvector extension enable.
- CloudWatch logs.

(Chi tiết tùy infra team — viết Terraform/CDK module riêng.)

## CI/CD ví dụ (GitHub Actions)

`.github/workflows/deploy.yml`:

```yaml
name: Deploy
on:
  push: { branches: [main] }

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '9.0.x' }
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release

      - name: Build & push image
        run: |
          docker build -t ghcr.io/${{ github.repository }}/salio-api:${{ github.sha }} .
          echo "${{ secrets.GHCR_TOKEN }}" | docker login ghcr.io -u ${{ github.actor }} --password-stdin
          docker push ghcr.io/${{ github.repository }}/salio-api:${{ github.sha }}

      - name: SSH deploy
        uses: appleboy/ssh-action@v1
        with:
          host: ${{ secrets.PROD_HOST }}
          username: ${{ secrets.PROD_USER }}
          key: ${{ secrets.PROD_SSH_KEY }}
          script: |
            cd /opt/salio
            export IMAGE_TAG=${{ github.sha }}
            docker compose -f docker-compose.prod.yml pull
            docker compose -f docker-compose.prod.yml up -d
```

## Monitoring

### Health endpoint cho LB

```http
GET /api/v1/health
→ 200 { "success": true, "data": { "status": "ok" } }
```

Mở rộng (gợi ý) — kiểm tra DB:

```csharp
[HttpGet]
public async Task<IActionResult> Get([FromServices] SalioDbContext db, CancellationToken ct)
{
    var dbOk = await db.Database.CanConnectAsync(ct);
    return Ok(ApiResponse<object>.Ok(new {
        status = dbOk ? "ok" : "degraded",
        db = dbOk,
        timestamp = DateTimeOffset.UtcNow
    }));
}
```

### Metrics

OpenTelemetry export Prometheus / Application Insights / Datadog:

```csharp
services.AddOpenTelemetry()
    .WithMetrics(b => b.AddAspNetCoreInstrumentation().AddPrometheusExporter())
    .WithTracing(b => b.AddAspNetCoreInstrumentation().AddNpgsql().AddOtlpExporter());
```

### Log aggregation

Serilog Sink cho Seq:

```csharp
.WriteTo.Seq("http://seq.internal:5341", apiKey: cfg["Seq:ApiKey"])
```

Hoặc Elasticsearch:

```csharp
.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://es:9200")) { IndexFormat = "salio-{0:yyyy.MM.dd}" })
```

## Backup strategy

### Daily logical backup

`cron`:

```bash
0 2 * * * docker exec salio-postgres pg_dump -U salio -d salio -F c | gzip > /backups/salio-$(date +\%Y\%m\%d).sql.gz
```

Giữ 30 ngày, sync lên S3.

### Point-in-time recovery (WAL)

Cần setup `archive_mode=on`, `archive_command` → object storage. Hoặc dùng managed service (RDS/Azure DB) có PITR built-in.

### Disaster recovery test

Hàng quý: restore backup vào staging, smoke test → verify backup khả dụng.

## Zero downtime deploy

- Có 2+ replica API behind load balancer.
- Rolling update: kéo new image, drain connection từ replica cũ.
- DB migration backward compatible (thêm column nullable, không drop ngay).
- Feature flag cho breaking change.

## Rollback plan

- Giữ 3 image tag gần nhất.
- Rollback chỉ là pull image cũ + restart.
- DB migration rollback: ưu tiên forward-only migration. Nếu phải rollback schema → manual SQL.

## Post-deploy verify

```bash
# Health
curl https://api.salio.com/api/v1/health

# Login với account test
curl -X POST https://api.salio.com/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@salio.com","password":"..."}'

# Check log
docker compose -f docker-compose.prod.yml logs --tail 200 api | grep -i error
```

Nếu fail → rollback theo plan trên.
