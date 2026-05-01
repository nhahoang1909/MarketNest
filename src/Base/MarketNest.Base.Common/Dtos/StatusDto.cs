namespace MarketNest.Base.Common;

/// <summary>
///     Lightweight timestamp DTO for "created/updated" audit display on list items.
/// </summary>
public record TimestampDto
{
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}

/// <summary>
///     DTO for entities that carry a status badge (e.g., orders, storefronts).
/// </summary>
public record StatusDto
{
    /// <summary>Machine-readable status code (e.g., "active", "pending").</summary>
    public required string Code { get; init; }

    /// <summary>Display label (e.g., "Active", "Pending Approval").</summary>
    public required string Label { get; init; }

    /// <summary>Optional CSS color class (e.g., "green", "yellow", "red").</summary>
    public string? Color { get; init; }
}

