using MarketNest.Base.Domain;

namespace MarketNest.Payments.Domain;

/// <summary>
///     Commission policy record (append-only log).
///     <c>StorefrontId == null</c> → platform default rate.
///     <c>StorefrontId != null</c> → per-seller override.
///     Owned by Payments module; stored in <c>payments.commission_policies</c>.
/// </summary>
public class CommissionPolicy : Entity<Guid>
{
    private const decimal MinRate = 0.00m;
    private const decimal MaxRate = 0.50m; // 50% hard cap

    /// <summary>StorefrontId of the seller this override applies to. Null = platform default.</summary>
    public Guid? StorefrontId { get; private set; }

    /// <summary>Commission rate as a decimal fraction. e.g. 0.10 = 10%.</summary>
    public decimal Rate { get; private set; }

    /// <summary>When this rate becomes effective.</summary>
    public DateTimeOffset EffectiveFrom { get; private set; }

    /// <summary>Admin who set this rate.</summary>
    public Guid SetByAdminId { get; private set; }

    private CommissionPolicy() { }

    /// <summary>Creates the platform-wide default commission policy record.</summary>
    public static Result<CommissionPolicy, Error> SetDefault(decimal rate, Guid adminId)
    {
        if (rate is < MinRate or > MaxRate)
            return Result<CommissionPolicy, Error>.Failure(
                new Error("COMMISSION.INVALID_RATE",
                    $"Commission rate must be between {MinRate:P0} and {MaxRate:P0}"));

        return Result<CommissionPolicy, Error>.Success(new CommissionPolicy
        {
            Id = Guid.NewGuid(),
            StorefrontId = null,
            Rate = rate,
            EffectiveFrom = DateTimeOffset.UtcNow,
            SetByAdminId = adminId
        });
    }

    /// <summary>Creates a per-seller commission override.</summary>
    public static Result<CommissionPolicy, Error> SetForStorefront(
        Guid storefrontId, decimal rate, DateTimeOffset effectiveFrom, Guid adminId)
    {
        if (rate is < MinRate or > MaxRate)
            return Result<CommissionPolicy, Error>.Failure(
                new Error("COMMISSION.INVALID_RATE",
                    $"Commission rate must be between {MinRate:P0} and {MaxRate:P0}"));

        return Result<CommissionPolicy, Error>.Success(new CommissionPolicy
        {
            Id = Guid.NewGuid(),
            StorefrontId = storefrontId,
            Rate = rate,
            EffectiveFrom = effectiveFrom,
            SetByAdminId = adminId
        });
    }
}

