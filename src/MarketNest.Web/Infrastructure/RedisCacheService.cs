using System.Text.Json;
using MarketNest.Base.Common;
using StackExchange.Redis;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Redis-backed implementation of <see cref="ICacheService" />.
///     Registered as Singleton in <c>Program.cs</c>.
///     Source of truth is always the DB — Redis is a read-through cache only.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private readonly IDatabase _db;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RedisCacheService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var value = await _db.StringGetAsync(key);
        if (!value.HasValue) return null;
        return JsonSerializer.Deserialize<T>((string)value!, SerializerOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default)
        where T : class
    {
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        await _db.StringSetAsync(key, json, ttl);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
        => await _db.KeyDeleteAsync(key);

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints().First());

        // Use SCAN (cursor-based) instead of KEYS — safe for production workloads.
        var keys = new List<RedisKey>();
        await foreach (var key in server.KeysAsync(pattern: $"{prefix}*"))
        {
            keys.Add(key);
            // Batch delete every 100 keys to avoid holding too many in memory
            if (keys.Count >= 100)
            {
                await _db.KeyDeleteAsync(keys.ToArray());
                keys.Clear();
            }
        }

        if (keys.Count > 0)
            await _db.KeyDeleteAsync(keys.ToArray());
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken ct = default) where T : class
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null) return cached;

        var value = await factory();
        await SetAsync(key, value, ttl, ct);
        return value;
    }
}
