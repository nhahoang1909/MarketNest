using MarketNest.Base.Common;

namespace MarketNest.Web.Infrastructure;

public static class SequenceServiceExtensions
{
    /// <summary>
    /// Registers <see cref="ISequenceService"/> as Singleton (PostgreSQL period-scoped sequences).
    /// Singleton is required because <c>_provisionedSequences</c> in-memory cache must be shared
    /// across all requests within the same app instance to avoid redundant DDL per request.
    /// </summary>
    public static IServiceCollection AddSequenceService(this IServiceCollection services)
    {
        services.AddSingleton<ISequenceService, PostgresSequenceProvider>();
        return services;
    }
}

