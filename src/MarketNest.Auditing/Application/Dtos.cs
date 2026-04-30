namespace MarketNest.Auditing.Application;

public record AuditLogDto
{
    public required Guid Id { get; init; }
    public required string EventType { get; init; }
    public Guid? ActorId { get; init; }
    public string? ActorEmail { get; init; }
    public string? ActorRole { get; init; }
    public string? EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public string? OldValues { get; init; }
    public string? NewValues { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
}

public record LoginEventDto
{
    public required Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public required string Email { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public required bool Success { get; init; }
    public string? FailureReason { get; init; }
    public required DateTimeOffset OccurredAt { get; init; }
}
