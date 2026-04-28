using Microsoft.Extensions.Configuration;

namespace MarketNest.Payments.Infrastructure;

/// <summary>DI registration for the Payments module.</summary>
public static class PaymentsServiceExtensions
{
    public static IServiceCollection AddPaymentsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<CommissionConfigService>();
        services.AddScoped<ICommissionConfig>(sp => sp.GetRequiredService<CommissionConfigService>());
        services.AddScoped<ICommissionConfigWriter>(sp => sp.GetRequiredService<CommissionConfigService>());

        return services;
    }
}

