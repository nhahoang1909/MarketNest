namespace MarketNest.Base.Common;

/// <summary>
///     Read contract for Commission configuration.
///     Implemented by <c>CommissionConfigService</c> in the Payments module.
/// </summary>
public interface ICommissionConfig
{
    /// <summary>Platform default commission rate. e.g. 0.10 = 10%.</summary>
    decimal DefaultRate { get; }

    /// <summary>
    ///     Returns the effective commission rate for the given seller.
    ///     Returns seller-specific override if one exists, otherwise the default rate.
    /// </summary>
    Task<decimal> GetRateForSellerAsync(Guid sellerId, CancellationToken ct = default);
}

