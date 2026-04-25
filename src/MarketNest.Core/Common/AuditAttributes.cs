namespace MarketNest.Core.Common;

/// <summary>
/// Marks an entity for automatic audit logging via EF Core SaveChanges interceptor.
/// When an entity with this attribute is created, updated, or deleted,
/// the change is recorded in the auditing schema automatically.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AuditableAttribute : Attribute;

/// <summary>
/// Marks a command for automatic audit logging via MediatR pipeline behavior.
/// After the command executes, an audit entry is recorded with the event type and entity info.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AuditedAttribute : Attribute
{
    public AuditedAttribute(string? eventType = null)
    {
        EventType = eventType;
    }

    /// <summary>Override the event type (e.g., "ORDER_CANCELLED"). Defaults to command name.</summary>
    public string? EventType { get; }

    /// <summary>The entity type being audited (e.g., "Order", "Product").</summary>
    public string? EntityType { get; init; }

    /// <summary>Whether to record audit entries for failed commands. Default: true.</summary>
    public bool AuditFailures { get; init; } = true;
}

