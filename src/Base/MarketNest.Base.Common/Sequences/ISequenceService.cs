namespace MarketNest.Base.Common;

/// <summary>
/// Generates deadlock-free, period-resettable running numbers via PostgreSQL SEQUENCE.
///
/// Key guarantees:
///   - Atomic: NEXTVAL never returns duplicates under any concurrency level
///   - Reset-safe: new period = new sequence object (no ALTER SEQUENCE race)
///   - Auto-provision: sequence is created on first call of each period (DDL cached)
///
/// Usage:
///   var number = await _seq.NextFormattedAsync(OrderSequences.OrderNumber, ct);
///   // → "ORD202604-00001"
/// </summary>
public interface ISequenceService
{
    /// <summary>
    /// Returns the next formatted running number for the current period.
    /// Auto-creates the period sequence if it doesn't exist yet.
    /// </summary>
    Task<string> NextFormattedAsync(
        SequenceDescriptor descriptor,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the next raw long value for the current period.
    /// Use when you need to format yourself or need the raw number.
    /// </summary>
    Task<long> NextValueAsync(
        SequenceDescriptor descriptor,
        CancellationToken ct = default);

    /// <summary>
    /// Lists all PG sequence names for a descriptor, filtered by period key prefix.
    /// Used by cleanup job. Pass empty string to list all periods.
    /// </summary>
    Task<IReadOnlyList<string>> ListSequenceNamesAsync(
        SequenceDescriptor descriptor,
        string periodKeyPrefix,
        CancellationToken ct = default);

    /// <summary>
    /// Drops a specific period sequence by its schema-qualified name.
    /// Only called by cleanup job for confirmed-stale periods.
    /// </summary>
    Task DropSequenceAsync(string schemaQualifiedName, CancellationToken ct = default);
}

