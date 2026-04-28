using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MarketNest.Notifications.Infrastructure;

/// <summary>DI stub for the Notifications module. Full implementation in subsequent sprints.</summary>
public static class NotificationsServiceExtensions
{
    public static IServiceCollection AddNotificationsModule(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}

