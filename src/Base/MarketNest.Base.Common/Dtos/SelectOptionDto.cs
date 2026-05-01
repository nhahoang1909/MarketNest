namespace MarketNest.Base.Common;

/// <summary>
///     Generic option DTO for dropdowns, multi-select lists, and autocomplete fields.
///     Can represent any entity by its Id and display Name.
/// </summary>
/// <typeparam name="TKey">The type of the identifier (usually <see cref="Guid"/>, <see cref="int"/>, or <see cref="string"/>).</typeparam>
public record SelectOptionDto<TKey>
{
    public required TKey Id { get; init; }
    public required string Name { get; init; }

    /// <summary>Optional secondary value (e.g., code, slug, or extra metadata).</summary>
    public string? Value { get; init; }

    /// <summary>Optional description shown as subtext or tooltip.</summary>
    public string? Description { get; init; }

    /// <summary>Whether this option is disabled/unavailable for selection.</summary>
    public bool Disabled { get; init; }
}

/// <summary>
///     Convenience alias for <see cref="SelectOptionDto{TKey}"/> with <see cref="Guid"/> key.
/// </summary>
public record SelectOptionDto : SelectOptionDto<Guid>;

/// <summary>
///     Convenience alias for <see cref="SelectOptionDto{TKey}"/> with <see cref="int"/> key.
///     Ideal for reference data (countries, categories, etc.).
/// </summary>
public record SelectOptionIntDto : SelectOptionDto<int>;

