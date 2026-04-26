namespace MarketNest.Base.Common;

/// <summary>
///     Records audit logs and login events. Phase 1: writes to "auditing" schema in shared DB.
///     Phase 3+: publishes to RabbitMQ; Audit Service consumes and persists.
/// </summary>
public interface IAuditService
{
    /// <summary>Record an entity change or business action.</summary>
    Task RecordAsync(AuditEntry entry, CancellationToken ct = default);

    /// <summary>Record a login attempt (success or failure).</summary>
    Task RecordLoginAsync(LoginEntry entry, CancellationToken ct = default);
}

/// <summary>Audit log entry for entity changes and business actions.</summary>
public record AuditEntry
{
    public required string EventType { get; init; }
    public Guid? ActorId { get; init; }
    public string? ActorEmail { get; init; }
    public string? ActorRole { get; init; }
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public object? OldValues { get; init; }
    public object? NewValues { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}

/// <summary>Login event entry for authentication tracking.</summary>
public record LoginEntry
{
    public required Guid? UserId { get; init; }
    public required string Email { get; init; }
    public required string IpAddress { get; init; }
    public required string UserAgent { get; init; }
    public required bool Success { get; init; }
    public string? FailureReason { get; init; }
}
