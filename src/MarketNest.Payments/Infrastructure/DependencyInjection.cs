using MarketNest.Payments.Application;
using Microsoft.Extensions.Configuration;

namespace MarketNest.Payments.Infrastructure;

/// <summary>DI registration for the Payments module.</summary>
public static class PaymentsServiceExtensions
{
    private const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddPaymentsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<PaymentsDbContext>(opts => opts.UseNpgsql(connectionString));
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<PaymentsDbContext>());

        services.AddScoped<CommissionConfigProvider>();
        services.AddScoped<ICommissionConfig>(sp => sp.GetRequiredService<CommissionConfigProvider>());
        services.AddScoped<ICommissionConfigWriter>(sp => sp.GetRequiredService<CommissionConfigProvider>());

        // Background job — singleton (no scoped dependencies)
        services.AddSingleton<IBackgroundJob, FinancialReconciliationJob>();

        return services;
    }
}

