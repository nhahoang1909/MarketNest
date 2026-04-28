namespace MarketNest.Base.Common;

/// <summary>
///     Write contract for Review Policy configuration.
///     Called by Admin module only; implemented by Reviews module.
/// </summary>
public interface IReviewPolicyConfigWriter
{
    Task<Result<Unit, Error>> UpdateAsync(UpdateReviewPolicyRequest request, CancellationToken ct = default);
}

/// <summary>Input for updating review policy settings.</summary>
public record UpdateReviewPolicyRequest(
    bool AllowReviewAfterDisputedOrder,
    int ReviewEditWindowHours,
    int MaxReviewBodyLength);

