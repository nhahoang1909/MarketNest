namespace MarketNest.Base.Common;

/// <summary>
/// Controls how often the running number counter resets to 1.
/// </summary>
public enum SequenceResetPeriod
{
    /// <summary>
    /// Sequence never resets. Runs continuously.
    /// Suitable for: SKUs, internal IDs, low-volume permanent sequences.
    /// Format: {PREFIX}-{Number}  e.g. SKU-00001
    /// </summary>
    Never = 0,

    /// <summary>
    /// Resets at the start of each calendar month.
    /// Suitable for: Orders, Invoices — high daily volume.
    /// Format: {PREFIX}{YYYYMM}-{Number}  e.g. ORD202604-00001
    /// </summary>
    Monthly = 1,

    /// <summary>
    /// Resets at the start of each calendar year.
    /// Suitable for: Payouts, Contracts — medium annual volume.
    /// Format: {PREFIX}{YYYY}-{Number}  e.g. PAY2026-00001
    /// </summary>
    Yearly = 2,
}

