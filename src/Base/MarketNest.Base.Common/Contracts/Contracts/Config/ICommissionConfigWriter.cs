namespace MarketNest.Base.Common;

/// <summary>
///     Write contract for Commission configuration.
///     Called by Admin module only; implemented by Payments module.
/// </summary>
public interface ICommissionConfigWriter
{
    /// <summary>Updates the platform-wide default commission rate.</summary>
    Task<Result<Unit, Error>> SetDefaultRateAsync(decimal rate, Guid adminId, CancellationToken ct = default);

    /// <summary>Sets a per-seller commission override.</summary>
    Task<Result<Unit, Error>> SetSellerOverrideAsync(
        Guid sellerId,
        decimal rate,
        DateTimeOffset effectiveFrom,
        Guid adminId,
        CancellationToken ct = default);

    /// <summary>Removes a per-seller override, restoring the default rate.</summary>
    Task<Result<Unit, Error>> RemoveSellerOverrideAsync(Guid sellerId, Guid adminId, CancellationToken ct = default);
}

