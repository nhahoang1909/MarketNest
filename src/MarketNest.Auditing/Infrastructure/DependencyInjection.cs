using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace MarketNest.Auditing.Infrastructure;

/// <summary>DI registration for the Auditing module.</summary>
public static class AuditingServiceExtensions
{
    private const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddAuditingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<AuditingDbContext>(opts => opts.UseNpgsql(connectionString));
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<AuditingDbContext>());

        services.AddScoped<AuditableInterceptor>();
        services.AddScoped<IAuditService, AuditService>();

        return services;
    }
}


