using System.Text.RegularExpressions;

namespace MarketNest.Base.Common;

/// <summary>
/// Immutable descriptor for a period-scoped PostgreSQL sequence.
/// Each module defines its own static instances.
/// The actual PG sequence name is computed per-period at runtime.
/// </summary>
public sealed partial record SequenceDescriptor
{
    /// <summary>
    /// The PostgreSQL schema this sequence lives in. Must match module schema.
    /// Example: "orders", "payments", "catalog"
    /// </summary>
    public string Schema { get; }

    /// <summary>
    /// Base name used to build the sequence object name.
    /// Example: "ord" → sequence "orders.seq_ord_202604"
    /// Must be lowercase alphanumeric + underscore only.
    /// </summary>
    public string BaseName { get; }

    /// <summary>
    /// Human-readable prefix embedded in the formatted number.
    /// Example: "ORD", "INV", "PAY"
    /// </summary>
    public string Prefix { get; }

    /// <summary>
    /// Zero-pad width for the numeric part.
    /// 5 → "00001" (max 99,999/period)
    /// 6 → "000001" (max 999,999/period)
    /// </summary>
    public int PadWidth { get; }

    /// <summary>
    /// How often the counter resets. Controls period key format in both
    /// the sequence name and the formatted output.
    /// </summary>
    public SequenceResetPeriod ResetPeriod { get; }

    private const int MinPadWidth = 4;
    private const int MaxPadWidth = 9;

    public SequenceDescriptor(
        string schema,
        string baseName,
        string prefix,
        int padWidth = 5,
        SequenceResetPeriod resetPeriod = SequenceResetPeriod.Monthly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        if (padWidth is < MinPadWidth or > MaxPadWidth)
            throw new ArgumentOutOfRangeException(nameof(padWidth), $"Must be {MinPadWidth}–{MaxPadWidth}.");

        if (!BaseNamePattern().IsMatch(baseName))
            throw new ArgumentException(
                "BaseName must be lowercase alphanumeric/underscore.", nameof(baseName));

        Schema = schema.ToLowerInvariant();
        BaseName = baseName.ToLowerInvariant();
        Prefix = prefix.ToUpperInvariant();
        PadWidth = padWidth;
        ResetPeriod = resetPeriod;
    }

    /// <summary>
    /// Computes the actual PostgreSQL sequence name for the given point in time.
    /// Always schema-qualified.
    /// </summary>
    public string GetSequenceName(DateTimeOffset asOf)
    {
        var periodKey = ResetPeriod switch
        {
            SequenceResetPeriod.Monthly => asOf.ToString("yyyyMM", System.Globalization.CultureInfo.InvariantCulture),
            SequenceResetPeriod.Yearly => asOf.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture),
            SequenceResetPeriod.Never => "permanent",
            _ => throw new InvalidOperationException($"Unknown period: {ResetPeriod}")
        };

        return $"{Schema}.seq_{BaseName}_{periodKey}";
    }

    /// <summary>
    /// Formats a raw sequence value into the running number string.
    /// </summary>
    public string Format(long value, DateTimeOffset asOf)
    {
        var padded = value.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(PadWidth, '0');

        return ResetPeriod switch
        {
            SequenceResetPeriod.Monthly => $"{Prefix}{asOf:yyyyMM}-{padded}",
            SequenceResetPeriod.Yearly => $"{Prefix}{asOf:yyyy}-{padded}",
            SequenceResetPeriod.Never => $"{Prefix}-{padded}",
            _ => throw new InvalidOperationException($"Unknown period: {ResetPeriod}")
        };
    }

    [GeneratedRegex(@"^[a-z0-9_]+$")]
    private static partial Regex BaseNamePattern();
}

