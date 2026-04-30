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
        string connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<AuditingDbContext>(opts => opts.UseNpgsql(connectionString));
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<AuditingDbContext>());

        services.AddScoped<AuditableInterceptor>();
        services.AddScoped<IAuditService, AuditService>();

        // MediatR pipeline behaviors — order matters: Performance runs first (outermost),
        // then AuditBehavior runs after the handler returns.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));

        return services;
    }
}


