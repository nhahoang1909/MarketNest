using MarketNest.Auditing.Application;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;
using MediatR;
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
        string writeConnection = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");

        // ReadConnection falls back to DefaultConnection when empty — Phase 2: point to read replica
        string readConnection = configuration.GetConnectionString("ReadConnection")
                                    is { Length: > 0 } rc ? rc : writeConnection;

        services.AddDbContext<AuditingDbContext>(opts => opts.UseNpgsql(writeConnection));
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<AuditingDbContext>());

        // Read context — NoTracking, no migrations
        services.AddDbContext<AuditingReadDbContext>(opts =>
            opts.UseNpgsql(readConnection)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        services.AddScoped<AuditableInterceptor>();
        services.AddScoped<IAuditService, AuditService>();

        // Query implementations
        services.AddScoped<IGetAuditLogsPagedQuery, AuditLogQuery>();
        services.AddScoped<IGetLoginEventsPagedQuery, LoginEventQuery>();

        // MediatR pipeline behaviors — order matters: Performance runs first (outermost),
        // then AuditBehavior runs after the handler returns.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));

        return services;
    }
}


