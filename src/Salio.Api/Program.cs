using System.Text;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Salio.Api.Common;
using Salio.Api.Middleware;
using Salio.Api.Services;
using Salio.Api.Swagger;
using Salio.Application;
using Salio.Application.Common.Interfaces;
using Salio.Infrastructure;
using Salio.Infrastructure.Configuration;
using Salio.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ───────────── Logging ─────────────
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/salio-.log", rollingInterval: RollingInterval.Day));

// ───────────── DI ─────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ───────────── Auth: JWT Bearer ─────────────
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt section missing");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        opt.SaveToken = true;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

// ───────────── API versioning ─────────────
builder.Services
    .AddApiVersioning(o =>
    {
        o.DefaultApiVersion = new ApiVersion(1, 0);
        o.AssumeDefaultVersionWhenUnspecified = true;
        o.ReportApiVersions = true;
        o.ApiVersionReader = ApiVersionReader.Combine(
            new UrlSegmentApiVersionReader(),
            new HeaderApiVersionReader("X-Api-Version"));
    })
    .AddApiExplorer(o =>
    {
        o.GroupNameFormat = "'v'VVV";
        o.SubstituteApiVersionInUrl = true;
    });

// ───────────── MVC ─────────────
builder.Services.AddControllers(o =>
    {
        // Tự động bọc mọi response chưa-wrap vào template { status, code, message, data }
        o.Filters.Add<ResponseWrapperFilter>();
    })
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// ModelState invalid (400) → trả về dưới dạng ApiResponse error (thay vì ProblemDetails mặc định)
builder.Services.Configure<Microsoft.AspNetCore.Mvc.ApiBehaviorOptions>(o =>
{
    o.InvalidModelStateResponseFactory = ctx =>
    {
        var errors = ctx.ModelState
            .Where(kv => kv.Value is { Errors.Count: > 0 })
            .Select(kv => new
            {
                field = kv.Key,
                errors = kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray(),
            });

        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new ApiResponse
        {
            Status = ApiStatus.Error,
            Code = StatusCodes.Status400BadRequest,
            Message = "Validation failed",
            Errors = new { code = "VALIDATION", details = errors },
            TraceId = ctx.HttpContext.TraceIdentifier,
        });
    };
});

// ───────────── CORS ─────────────
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("AllowFrontend", p => p
        .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

// ───────────── Swagger / OpenAPI ─────────────
builder.Services.AddEndpointsApiExplorer();

// Tự sinh SwaggerDoc cho từng API version (v1, v2, …) thông qua IApiVersionDescriptionProvider
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

builder.Services.AddSwaggerGen(c =>
{
    // ── Bao gồm toàn bộ XML comment từ Api / Application / Domain ──
    var baseDir = AppContext.BaseDirectory;
    foreach (var xmlFile in new[]
             {
                 "Salio.Api.xml",
                 "Salio.Application.xml",
                 "Salio.Domain.xml",
             })
    {
        var xmlPath = Path.Combine(baseDir, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }
    }

    // ── Bearer auth ──
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập JWT access token theo định dạng: `Bearer {token}`",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    });

    // Chỉ gắn yêu cầu Bearer ở endpoint có [Authorize] (bỏ qua [AllowAnonymous])
    c.OperationFilter<AuthorizeCheckOperationFilter>();

    // ── Tổ chức tag theo controller ──
    c.TagActionsBy(api => new[] { api.ActionDescriptor.RouteValues["controller"] ?? "Default" });
    c.DocInclusionPredicate((_, _) => true);

    // Tránh xung đột schema khi có DTO trùng tên ở các namespace khác nhau
    c.CustomSchemaIds(t => t.FullName?.Replace("+", ".") ?? t.Name);

    // Enum trả về dưới dạng string cho dễ đọc
    c.UseAllOfToExtendReferenceSchemas();
    c.SupportNonNullableReferenceTypes();
});

var app = builder.Build();

// ───────────── Migrate & seed ─────────────
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<SalioDbContext>();
        if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
        {
            await db.Database.MigrateAsync();
        }
        if (app.Configuration.GetValue<bool>("Database:AutoSeed"))
        {
            await SalioDbSeeder.SeedAsync(db);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Database migration/seed skipped — could not connect to the database. The API will still start.");
    }
}

// ───────────── Pipeline ─────────────
app.UseSerilogRequestLogging();
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Bật Swagger ở cả Development và khi cấu hình Swagger:Enabled = true
var swaggerEnabled = app.Environment.IsDevelopment()
                     || app.Configuration.GetValue<bool>("Swagger:Enabled");

if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

        // Tạo endpoint UI cho mỗi version API được khám phá
        foreach (var description in provider.ApiVersionDescriptions
                                            .OrderByDescending(d => d.ApiVersion))
        {
            c.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                $"Salio API {description.GroupName.ToUpperInvariant()}");
        }

        c.RoutePrefix = "swagger";
        c.DocumentTitle = "Salio Sales AI API — Swagger UI";
        c.DefaultModelsExpandDepth(-1);
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.EnableFilter();
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
