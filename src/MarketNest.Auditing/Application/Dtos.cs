namespace MarketNest.Auditing.Application;

public record AuditLogDto
{
    public Guid Id { get; init; }
    public string EventType { get; init; } = null!;
    public Guid? ActorId { get; init; }
    public string? ActorEmail { get; init; }
    public string? ActorRole { get; init; }
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public string? OldValues { get; init; }
    public string? NewValues { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

public record LoginEventDto
{
    public Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public string Email { get; init; } = null!;
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public bool Success { get; init; }
    public string? FailureReason { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

