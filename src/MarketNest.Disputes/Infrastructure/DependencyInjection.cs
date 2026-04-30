using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketNest.Disputes.Infrastructure;

/// <summary>DI stub for the Disputes module. Full implementation in subsequent sprints.</summary>
public static class DisputesServiceExtensions
{
    public static IServiceCollection AddDisputesModule(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}

