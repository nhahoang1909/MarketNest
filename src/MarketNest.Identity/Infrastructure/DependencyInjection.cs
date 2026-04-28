using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketNest.Identity.Infrastructure;

/// <summary>DI stub for the Identity module. Full implementation in subsequent sprints.</summary>
public static class IdentityServiceExtensions
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}

