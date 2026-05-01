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
    private const string WriteConnectionName = "DefaultConnection";
    private const string ReadConnectionName = "ReadConnection";

    public static IServiceCollection AddAdminModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string writeConnection = configuration.GetConnectionString(WriteConnectionName)
            ?? throw new InvalidOperationException(
                $"Connection string '{WriteConnectionName}' is not configured.");

        // ReadConnection falls back to DefaultConnection when empty — Phase 2: point to read replica
        string readConnection = configuration.GetConnectionString(ReadConnectionName)
                                    is { Length: > 0 } rc ? rc : writeConnection;

        // Write context — tracked by DatabaseInitializer for migrations
        services.AddDbContext<AdminDbContext>(opts => opts.UseNpgsql(writeConnection));
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<AdminDbContext>());

        // Read context — NoTracking, no migrations; uses read replica when ReadConnection is set
        services.AddDbContext<AdminReadDbContext>(opts =>
            opts.UseNpgsql(readConnection)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        // Reference Data read service (Tier 1) — scoped because AdminReadDbContext is scoped
        services.AddScoped<IReferenceDataReadService, ReferenceDataReadService>();

        // Announcement — repository + queries
        services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
        services.AddScoped<IGetAnnouncementsPagedQuery, AnnouncementQuery>();
        services.AddScoped<IGetActiveAnnouncementsQuery, AnnouncementQuery>();

        // Background job — singleton because it has no scoped dependencies
        services.AddSingleton<IBackgroundJob, TestTimerJob>();

        return services;
    }
}

