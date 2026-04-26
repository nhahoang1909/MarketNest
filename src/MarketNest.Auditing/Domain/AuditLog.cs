using MarketNest.Base.Domain;

namespace MarketNest.Auditing.Domain;

/// <summary>
///     Append-only audit log entry. Records entity changes and business actions.
/// </summary>
public class AuditLog : Entity<Guid>
{
    private AuditLog()
    {
    }

    public string EventType { get; private set; } = null!;
    public Guid? ActorId { get; private set; }
    public string? ActorEmail { get; private set; }
    public string? ActorRole { get; private set; }
    public string? EntityType { get; private set; }
    public Guid? EntityId { get; private set; }
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public string? Metadata { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    public static AuditLog Create(
        string eventType,
        Guid? actorId,
        string? actorEmail,
        string? actorRole,
        string? entityType,
        Guid? entityId,
        string? oldValues,
        string? newValues,
        string? metadata)
    {
        return new AuditLog
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            ActorId = actorId,
            ActorEmail = actorEmail,
            ActorRole = actorRole,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            Metadata = metadata,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }
}
