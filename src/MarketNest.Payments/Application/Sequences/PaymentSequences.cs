using MarketNest.Base.Common;

namespace MarketNest.Payments.Application;

/// <summary>
/// Sequence descriptors for the Payments module.
/// All sequences use the "payments" PostgreSQL schema.
/// </summary>
public static class PaymentSequences
{
    /// <summary>
    /// Yearly-reset payout number.
    /// Output: PAY2026-00001
    /// Capacity: 99,999 payouts/year
    /// </summary>
    public static readonly SequenceDescriptor PayoutNumber = new(
        schema: TableConstants.Schema.Payments,
        baseName: "pay",
        prefix: "PAY",
        padWidth: 5,
        resetPeriod: SequenceResetPeriod.Yearly);
}

