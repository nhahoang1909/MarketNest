using MarketNest.Admin.Application;
using MarketNest.Base.Utility;
using Microsoft.Extensions.Configuration;

namespace MarketNest.Admin.Infrastructure;

/// <summary>
///     DI registration for the Admin module.
///     Called from <c>Program.cs</c> in <c>MarketNest.Web</c>.
/// </summary>
public static class AdminServiceExtensions
{
    private const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddAdminModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");

        // Write context — tracked by DatabaseInitializer for migrations
        services.AddDbContext<AdminDbContext>(opts => opts.UseNpgsql(connectionString));
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<AdminDbContext>());

        // Read context — NoTracking, no migrations
        services.AddDbContext<AdminReadDbContext>(opts =>
            opts.UseNpgsql(connectionString)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        // Reference Data read service (Tier 1) — scoped because AdminReadDbContext is scoped
        services.AddScoped<IReferenceDataReadService, ReferenceDataReadService>();

        // Background job — singleton because it has no scoped dependencies
        services.AddSingleton<IBackgroundJob, TestTimerJob>();

        return services;
    }
}

