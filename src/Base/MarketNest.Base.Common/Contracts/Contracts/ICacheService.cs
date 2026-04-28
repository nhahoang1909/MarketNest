namespace MarketNest.Base.Common;

/// <summary>
///     Distributed cache abstraction. Implemented by <c>RedisCacheService</c> in the Web host.
///     Source of truth is always the DB — cache is a read-through performance layer only.
/// </summary>
public interface ICacheService
{
    /// <summary>Returns the cached value or <c>null</c> if not found.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    /// <summary>Stores a value with an optional TTL.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) where T : class;

    /// <summary>Removes a single cache entry.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Removes all cache entries whose keys start with <paramref name="prefix" />.</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);

    /// <summary>
    ///     Returns the cached value if present; otherwise calls <paramref name="factory" />,
    ///     stores the result, and returns it. Null results from the factory are not cached.
    /// </summary>
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null, CancellationToken ct = default)
        where T : class;
}

