using MarketNest.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Payments.Infrastructure;

/// <summary>
///     Implements both <see cref="ICommissionConfig" /> (read) and
///     <see cref="ICommissionConfigWriter" /> (write) contracts.
///     Registered as scoped in <c>AddPaymentsModule</c>.
/// </summary>
internal sealed class CommissionConfigService(
    PaymentsDbContext db,
    ICacheService cache) : ICommissionConfig, ICommissionConfigWriter
{
    private const decimal FallbackDefaultRate = 0.10m; // 10%

    // ── ICommissionConfig (read) ──────────────────────────────────────────

    public decimal DefaultRate => GetDefaultRateFromCache();

    public async Task<decimal> GetRateForSellerAsync(Guid sellerId, CancellationToken ct = default)
    {
        var cacheKey = CacheKeys.BusinessConfig.CommissionForSeller(sellerId);
        var cached = await cache.GetAsync<CommissionRateSnapshot>(cacheKey, ct);
        if (cached is not null) return cached.Rate;

        // Latest override for this seller (most recent effective_from)
        var override_ = await db.CommissionPolicies
            .Where(x => x.StorefrontId == sellerId && x.EffectiveFrom <= DateTimeOffset.UtcNow)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

        var rate = override_?.Rate ?? DefaultRate;
        await cache.SetAsync(cacheKey, new CommissionRateSnapshot(rate), CacheKeys.Ttl.BusinessConfig, ct);
        return rate;
    }

    // ── ICommissionConfigWriter (write) ───────────────────────────────────

    public async Task<Result<Unit, Error>> SetDefaultRateAsync(
        decimal rate, Guid adminId, CancellationToken ct = default)
    {
        var result = CommissionPolicy.SetDefault(rate, adminId);
        if (result.IsFailure) return Result<Unit, Error>.Failure(result.Error);

        db.CommissionPolicies.Add(result.Value);
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.BusinessConfig.CommissionDefault, ct);
        return Result.Success();
    }

    public async Task<Result<Unit, Error>> SetSellerOverrideAsync(
        Guid sellerId, decimal rate, DateTimeOffset effectiveFrom, Guid adminId, CancellationToken ct = default)
    {
        var result = CommissionPolicy.SetForStorefront(sellerId, rate, effectiveFrom, adminId);
        if (result.IsFailure) return Result<Unit, Error>.Failure(result.Error);

        db.CommissionPolicies.Add(result.Value);
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.BusinessConfig.CommissionForSeller(sellerId), ct);
        return Result.Success();
    }

    public async Task<Result<Unit, Error>> RemoveSellerOverrideAsync(
        Guid sellerId, Guid adminId, CancellationToken ct = default)
    {
        // "Removing" an override = inserting a new record with the default rate
        return await SetSellerOverrideAsync(sellerId, DefaultRate, DateTimeOffset.UtcNow, adminId, ct);
    }

    // ── Private helpers ───────────────────────────────────────────────────

    private decimal _cachedDefaultRate = -1m;

    private decimal GetDefaultRateFromCache()
    {
        if (_cachedDefaultRate >= 0m) return _cachedDefaultRate;

        var latest = db.CommissionPolicies
            .Where(x => x.StorefrontId == null)
            .OrderByDescending(x => x.EffectiveFrom)
            .FirstOrDefault();

        _cachedDefaultRate = latest?.Rate ?? FallbackDefaultRate;
        return _cachedDefaultRate;
    }
}

internal sealed record CommissionRateSnapshot(decimal Rate);

