using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Salio.Application.Common.Interfaces;
using Salio.Infrastructure.Configuration;
using Salio.Infrastructure.Persistence;
using Salio.Infrastructure.Services;

namespace Salio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connStr = config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default not configured");

        services.AddDbContext<SalioDbContext>(opt =>
            opt.UseNpgsql(connStr, npg =>
            {
                npg.UseVector();
                npg.MigrationsHistoryTable("__ef_migrations", "salio");
            }));

        services.AddScoped<ISalioDbContext>(sp => sp.GetRequiredService<SalioDbContext>());

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IPermissionChecker, PermissionChecker>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        return services;
    }
}

public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
