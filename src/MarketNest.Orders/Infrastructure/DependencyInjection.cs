using MarketNest.Orders.Domain;
using Microsoft.Extensions.Configuration;

namespace MarketNest.Orders.Infrastructure;

/// <summary>DI registration for the Orders module.</summary>
public static class OrdersServiceExtensions
{
    private const string ConnectionStringName = "DefaultConnection";

    public static IServiceCollection AddOrdersModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured.");

        services.AddDbContext<OrdersDbContext>(opts => opts.UseNpgsql(connectionString));
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<OrdersDbContext>());

        // Business Config — Tier 2 (IOrderPolicyConfig + IOrderPolicyConfigWriter → same singleton service)
        services.AddScoped<OrderPolicyConfigService>();
        services.AddScoped<IOrderPolicyConfig>(sp => sp.GetRequiredService<OrderPolicyConfigService>());
        services.AddScoped<IOrderPolicyConfigWriter>(sp => sp.GetRequiredService<OrderPolicyConfigService>());

        return services;
    }
}

