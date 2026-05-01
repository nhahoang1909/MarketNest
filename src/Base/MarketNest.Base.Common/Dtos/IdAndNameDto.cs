namespace MarketNest.Base.Common;

/// <summary>
///     Lightweight DTO returning only Id and Name — the minimal projection
///     for lookup lists, autocomplete, and cross-module references.
/// </summary>
public record IdAndNameDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
}

/// <summary>
///     <see cref="IdAndNameDto"/> variant with <see cref="int"/> key.
///     Ideal for reference data entities with integer PKs.
/// </summary>
public record IdAndNameIntDto
{
    public required int Id { get; init; }
    public required string Name { get; init; }
}

