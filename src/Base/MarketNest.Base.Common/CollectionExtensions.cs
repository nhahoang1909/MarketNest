namespace MarketNest.Base.Common;

/// <summary>
///     Extension methods for collections (IEnumerable, IReadOnlyList, etc.).
/// </summary>
public static class CollectionExtensions
{
    // ── Null-Safe Checks ────────────────────────────────────────────

    /// <summary>Returns true if the collection is null or has no elements.</summary>
    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? source)
        => source is null || !source.Any();

    /// <summary>Returns true if the collection is null or has no elements.</summary>
    public static bool IsNullOrEmpty<T>(this IReadOnlyCollection<T>? source)
        => source is null || source.Count == 0;

    /// <summary>Returns an empty enumerable if the source is null.</summary>
    public static IEnumerable<T> OrEmpty<T>(this IEnumerable<T>? source)
        => source ?? Enumerable.Empty<T>();

    // ── Batching ────────────────────────────────────────────────────

    /// <summary>
    ///     Splits a sequence into batches of the specified size.
    ///     The last batch may contain fewer items.
    /// </summary>
    public static IEnumerable<IReadOnlyList<T>> Batch<T>(this IEnumerable<T> source, int batchSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

        var batch = new List<T>(batchSize);
        foreach (T item in source)
        {
            batch.Add(item);
            if (batch.Count == batchSize)
            {
                yield return batch;
                batch = new List<T>(batchSize);
            }
        }

        if (batch.Count > 0)
            yield return batch;
    }


    // ── ForEach ─────────────────────────────────────────────────────

    /// <summary>Executes an action on each element of the sequence.</summary>
    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        foreach (T item in source)
            action(item);
    }

    /// <summary>Executes an action on each element with its zero-based index.</summary>
    public static void ForEach<T>(this IEnumerable<T> source, Action<T, int> action)
    {
        int index = 0;
        foreach (T item in source)
            action(item, index++);
    }

    // ── Safe Operations ─────────────────────────────────────────────

    /// <summary>
    ///     Returns the element at the given index, or <paramref name="defaultValue"/> if out of range.
    ///     Prefer this over direct indexer access (<c>list[index]</c>) to avoid
    ///     <see cref="ArgumentOutOfRangeException"/> when the index may be out of bounds.
    /// </summary>
    public static T? ElementAtOrDefault<T>(this IReadOnlyList<T> source, int index, T? defaultValue = default)
        => index >= 0 && index < source.Count ? source[index] : defaultValue;

    // ── Safe Dictionary Access ───────────────────────────────────────

    /// <summary>
    ///     Returns the value associated with <paramref name="key"/>, or
    ///     <paramref name="defaultValue"/> if the key is not present.
    ///     <para>
    ///     Prefer this over the dictionary indexer (<c>dict[key]</c>), which throws
    ///     <see cref="System.Collections.Generic.KeyNotFoundException"/> on a missing key
    ///     and crashes the request.
    ///     </para>
    /// </summary>
    public static TValue? GetValueOrDefault<TKey, TValue>(
        this IReadOnlyDictionary<TKey, TValue> source,
        TKey key,
        TValue? defaultValue = default)
        => source.TryGetValue(key, out var value) ? value : defaultValue;

    /// <summary>
    ///     Returns the value associated with <paramref name="key"/>, or
    ///     <paramref name="defaultValue"/> if the key is not present.
    ///     <para>
    ///     Prefer this over the dictionary indexer (<c>dict[key]</c>), which throws
    ///     <see cref="System.Collections.Generic.KeyNotFoundException"/> on a missing key
    ///     and crashes the request.
    ///     </para>
    /// </summary>
    public static TValue? GetValueOrDefault<TKey, TValue>(
        this IDictionary<TKey, TValue> source,
        TKey key,
        TValue? defaultValue = default)
        => source.TryGetValue(key, out var value) ? value : defaultValue;

    /// <summary>
    ///     Attempts to retrieve the value for <paramref name="key"/> and returns
    ///     a named tuple <c>(bool Found, TValue? Value)</c> for pattern-matching.
    ///     <para>
    ///     Prefer this over the dictionary indexer (<c>dict[key]</c>), which throws
    ///     <see cref="System.Collections.Generic.KeyNotFoundException"/> on a missing key
    ///     and crashes the request.
    ///     </para>
    /// </summary>
    /// <example>
    /// <code>
    /// var (found, price) = configMap.TryGet("shipping_fee");
    /// if (found) ApplyFee(price!);
    /// </code>
    /// </example>
    public static (bool Found, TValue? Value) TryGet<TKey, TValue>(
        this IReadOnlyDictionary<TKey, TValue> source,
        TKey key)
        => source.TryGetValue(key, out var value) ? (true, value) : (false, default);

    /// <summary>
    ///     Attempts to retrieve the value for <paramref name="key"/> and returns
    ///     a named tuple <c>(bool Found, TValue? Value)</c> for pattern-matching.
    ///     <para>
    ///     Prefer this over the dictionary indexer (<c>dict[key]</c>), which throws
    ///     <see cref="System.Collections.Generic.KeyNotFoundException"/> on a missing key
    ///     and crashes the request.
    ///     </para>
    /// </summary>
    public static (bool Found, TValue? Value) TryGet<TKey, TValue>(
        this IDictionary<TKey, TValue> source,
        TKey key)
        => source.TryGetValue(key, out var value) ? (true, value) : (false, default);

    // ── String Joining ──────────────────────────────────────────────

    /// <summary>
    ///     Joins elements into a string with the specified separator, filtering out nulls and empties.
    /// </summary>
    public static string JoinNonEmpty(this IEnumerable<string?> source, string separator = ", ")
        => string.Join(separator, source.Where(s => !string.IsNullOrWhiteSpace(s)));
}

