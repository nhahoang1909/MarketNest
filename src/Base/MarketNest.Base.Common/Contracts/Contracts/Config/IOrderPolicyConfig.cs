namespace MarketNest.Base.Common;

/// <summary>
///     Read contract for Order Policy configuration.
///     Implemented by <c>OrderPolicyConfigService</c> in the Orders module.
///     All properties are backed by a Redis-cached singleton DB row.
/// </summary>
public interface IOrderPolicyConfig
{
    /// <summary>Hours a seller has to confirm a new order before it auto-cancels. Default: 48.</summary>
    int SellerConfirmWindowHours { get; }

    /// <summary>Days after shipping before the order auto-transitions to Delivered. Default: 30.</summary>
    int AutoDeliverAfterShippedDays { get; }

    /// <summary>Days after Delivered before the order auto-completes (buyer confirmation window). Default: 3.</summary>
    int AutoCompleteAfterDeliveredDays { get; }

    /// <summary>Days after Delivered within which the buyer can open a dispute. Default: 3.</summary>
    int DisputeWindowAfterDeliveredDays { get; }
}

