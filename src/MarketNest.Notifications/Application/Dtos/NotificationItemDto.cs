namespace MarketNest.Notifications.Application;

/// <summary>DTO for a single notification inbox item.</summary>
public record NotificationItemDto
{
    public required Guid Id { get; init; }
    public required string TemplateKey { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
    public string? ActionUrl { get; init; }
    public required bool IsRead { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

