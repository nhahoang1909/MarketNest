namespace MarketNest.Base.Common;

/// <summary>
///     Read contract for Review Policy configuration.
///     Implemented by <c>ReviewPolicyConfigService</c> in the Reviews module.
/// </summary>
public interface IReviewPolicyConfig
{
    /// <summary>Whether buyers can leave a review on a disputed (not fully resolved) order. Default: false.</summary>
    bool AllowReviewAfterDisputedOrder { get; }

    /// <summary>Hours within which a buyer can edit their review after posting. Default: 24.</summary>
    int ReviewEditWindowHours { get; }

    /// <summary>Maximum length (characters) of a review body. Default: 1000.</summary>
    int MaxReviewBodyLength { get; }
}

