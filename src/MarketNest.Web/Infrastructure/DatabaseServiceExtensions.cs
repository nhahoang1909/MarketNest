using MarketNest.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Web.Infrastructure;

/// <summary>
/// Extension methods for registering <see cref="DatabaseInitializer"/> and auto-discovering
/// <see cref="IDataSeeder"/> implementations from module assemblies.
/// </summary>
public static class DatabaseServiceExtensions
{
    /// <summary>
    /// Registers the <see cref="DatabaseInitializer"/> as a singleton and auto-discovers
    /// all <see cref="IDataSeeder"/> implementations from the provided assemblies.
    /// </summary>
    public static IServiceCollection AddDatabaseInitializer<TDbContext>(
        this IServiceCollection services,
        params System.Reflection.Assembly[] seederAssemblies)
        where TDbContext : DbContext
    {
        // Register DbContext as the base DbContext so DatabaseInitializer can resolve it generically
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<TDbContext>());

        // Register the initializer itself
        services.AddSingleton<DatabaseInitializer>();

        // Auto-discover and register all IDataSeeder implementations from provided assemblies
        foreach (var assembly in seederAssemblies)
        {
            var seederTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsClass: true }
                            && typeof(IDataSeeder).IsAssignableFrom(t));

            foreach (var seederType in seederTypes)
            {
                services.AddScoped(typeof(IDataSeeder), seederType);
            }
        }

        return services;
    }
}
