using MarketNest.Base.Utility;
using MarketNest.Promotions.Application;
using Microsoft.Extensions.Configuration;

namespace MarketNest.Promotions.Infrastructure;

/// <summary>DI registration for the Promotions module.</summary>
public static class PromotionsServiceExtensions
{
    private const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddPromotionsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");

        // Write context — tracked by DatabaseInitializer for migrations
        services.AddDbContext<PromotionsDbContext>(opts => opts.UseNpgsql(connectionString));
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<PromotionsDbContext>());

        // Read context — NoTracking, no migrations
        services.AddDbContext<PromotionsReadDbContext>(opts =>
            opts.UseNpgsql(connectionString)
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        // Background job — scoped because it depends on scoped IVoucherRepository
        services.AddScoped<IBackgroundJob, VoucherExpiryJob>();

        return services;
    }
}

