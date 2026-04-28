using Microsoft.Extensions.Configuration;

namespace MarketNest.Admin.Infrastructure;

/// <summary>
///     DI registration for the Admin module.
///     Called from <c>Program.cs</c> in <c>MarketNest.Web</c>.
/// </summary>
public static class AdminServiceExtensions
{
    public static IServiceCollection AddAdminModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Reference Data read service (Tier 1) — scoped because AdminReadDbContext is scoped
        services.AddScoped<IReferenceDataReadService, ReferenceDataReadService>();

        return services;
    }
}

