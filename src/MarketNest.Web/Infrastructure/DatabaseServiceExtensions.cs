using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Extension methods for registering <see cref="DatabaseInitializer" />, <see cref="DatabaseTracker" />,
///     module DbContexts as <see cref="IModuleDbContext" />, and auto-discovering <see cref="IDataSeeder" />
///     implementations from module assemblies.
/// </summary>
public static class DatabaseServiceExtensions
{
    /// <summary>
    ///     Registers the database initialization infrastructure:
    ///     <list type="bullet">
    ///         <item><see cref="DatabaseTracker" /> — manages tracking tables for migration hashes and seed versions</item>
    ///         <item><see cref="DatabaseInitializer" /> — orchestrates migrations and seeders on startup</item>
    ///         <item>Auto-discovers all <see cref="IDataSeeder" /> implementations from the provided assemblies</item>
    ///     </list>
    /// </summary>
    /// <remarks>
    ///     Module DbContexts must be registered separately via <see cref="AddModuleDbContext{TContext}" />
    ///     before calling this method.
    /// </remarks>
    public static IServiceCollection AddDatabaseInitializer(
        this IServiceCollection services,
        params Assembly[] seederAssemblies)
    {
        // Register tracking and initialization services
        services.AddSingleton<DatabaseTracker>();
        services.AddSingleton<DatabaseInitializer>();

        // Register the UpdateTokenInterceptor as singleton (stateless — auto-rotates concurrency tokens)
        services.AddSingleton<UpdateTokenInterceptor>();

        // Register TrackableInterceptor as singleton — stateless; resolves IRuntimeContext per-save
        // from the DbContext's scoped service provider, so the user stamp is always request-scoped.
        services.AddSingleton<TrackableInterceptor>();

        // Register SoftDeleteInterceptor as singleton — converts EntityState.Deleted → Modified
        // for ISoftDeletable entities, preventing physical row deletion.
        services.AddSingleton<SoftDeleteInterceptor>();

        // Auto-discover and register all IDataSeeder implementations from provided assemblies
        foreach (Assembly assembly in seederAssemblies)
        {
            IEnumerable<Type> seederTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsClass: true }
                            && typeof(IDataSeeder).IsAssignableFrom(t));

            foreach (Type seederType in seederTypes) services.AddScoped(typeof(IDataSeeder), seederType);
        }

        return services;
    }

    /// <summary>
    ///     Registers a module's DbContext and also exposes it as <see cref="IModuleDbContext" />
    ///     so <see cref="DatabaseInitializer" /> can discover and iterate all module contexts.
    ///     Automatically adds the <see cref="UpdateTokenInterceptor" /> for optimistic concurrency support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">EF Core DbContext options builder (connection string, etc.).</param>
    /// <typeparam name="TContext">
    ///     The module's DbContext type. Must implement <see cref="IModuleDbContext" />.
    /// </typeparam>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureOptions)
        where TContext : DbContext, IModuleDbContext
    {
        services.AddDbContext<TContext>((sp, opts) =>
        {
            configureOptions(opts);

            // Add interceptors for all write-side module DbContexts
            var updateTokenInterceptor = sp.GetRequiredService<UpdateTokenInterceptor>();
            var trackableInterceptor   = sp.GetRequiredService<TrackableInterceptor>();
            var softDeleteInterceptor  = sp.GetRequiredService<SoftDeleteInterceptor>();
            opts.AddInterceptors(updateTokenInterceptor, trackableInterceptor, softDeleteInterceptor);
        });

        // Register as IModuleDbContext so DatabaseInitializer can enumerate all modules
        services.AddScoped<IModuleDbContext>(sp => sp.GetRequiredService<TContext>());

        return services;
    }
}
