using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketNest.Cart.Infrastructure;

/// <summary>DI stub for the Cart module. Full implementation in subsequent sprints.</summary>
public static class CartServiceExtensions
{
    public static IServiceCollection AddCartModule(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}

