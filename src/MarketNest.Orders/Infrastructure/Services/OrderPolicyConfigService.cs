using MarketNest.Orders.Domain;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Orders.Infrastructure;

/// <summary>
///     Implements both the read (<see cref="IOrderPolicyConfig" />) and
///     write (<see cref="IOrderPolicyConfigWriter" />) contracts for order policy configuration.
///     Registered as scoped in <c>AddOrdersModule</c>.
/// </summary>
internal sealed class OrderPolicyConfigService(
    OrdersDbContext db,
    ICacheService cache) : IOrderPolicyConfig, IOrderPolicyConfigWriter
{
    // ── IOrderPolicyConfig (read) ─────────────────────────────────────────

    public int SellerConfirmWindowHours
        => GetSnapshot().SellerConfirmWindowHours;

    public int AutoDeliverAfterShippedDays
        => GetSnapshot().AutoDeliverAfterShippedDays;

    public int AutoCompleteAfterDeliveredDays
        => GetSnapshot().AutoCompleteAfterDeliveredDays;

    public int DisputeWindowAfterDeliveredDays
        => GetSnapshot().DisputeWindowAfterDeliveredDays;

    // ── IOrderPolicyConfigWriter (write) ──────────────────────────────────

    public async Task<Result<Unit, Error>> UpdateAsync(
        UpdateOrderPolicyRequest request, CancellationToken ct = default)
    {
        OrderPolicyConfig config = await db.OrderPolicyConfigs
            .FirstOrDefaultAsync(ct) ?? OrderPolicyConfig.CreateDefault();

        // Placeholder — in Phase 2 inject ICurrentUserService to get real admin ID
        var result = config.Update(request, Guid.Empty);
        if (result.IsFailure) return result;

        db.OrderPolicyConfigs.Update(config);
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKeys.BusinessConfig.OrderPolicy, ct);
        return Result.Success();
    }

    // ── Private: load from cache or DB ───────────────────────────────────

    private OrderPolicyConfigSnapshot _snapshot = default!;

    private OrderPolicyConfigSnapshot GetSnapshot()
    {
        if (_snapshot is not null) return _snapshot;

        // Synchronous load during the first property access within the same scope.
        // In practice this runs once per request since the service is scoped.
        var config = db.OrderPolicyConfigs.FirstOrDefault()
            ?? OrderPolicyConfig.CreateDefault();

        _snapshot = new OrderPolicyConfigSnapshot(
            config.SellerConfirmWindowHours,
            config.AutoDeliverAfterShippedDays,
            config.AutoCompleteAfterDeliveredDays,
            config.DisputeWindowAfterDeliveredDays);

        return _snapshot;
    }
}

/// <summary>Immutable snapshot used for synchronous property access.</summary>
internal sealed record OrderPolicyConfigSnapshot(
    int SellerConfirmWindowHours,
    int AutoDeliverAfterShippedDays,
    int AutoCompleteAfterDeliveredDays,
    int DisputeWindowAfterDeliveredDays);

