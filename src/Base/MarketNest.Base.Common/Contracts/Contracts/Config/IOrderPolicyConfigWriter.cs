namespace MarketNest.Base.Common;

/// <summary>
///     Write contract for Order Policy configuration.
///     Called by Admin module only; implemented by Orders module.
/// </summary>
public interface IOrderPolicyConfigWriter
{
    Task<Result<Unit, Error>> UpdateAsync(UpdateOrderPolicyRequest request, CancellationToken ct = default);
}

/// <summary>Input for updating all order policy windows at once.</summary>
public record UpdateOrderPolicyRequest(
    int SellerConfirmWindowHours,
    int AutoDeliverAfterShippedDays,
    int AutoCompleteAfterDeliveredDays,
    int DisputeWindowAfterDeliveredDays);

