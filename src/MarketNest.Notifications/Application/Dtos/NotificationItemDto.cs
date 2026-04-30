namespace MarketNest.Notifications.Application;

/// <summary>DTO for a single notification inbox item.</summary>
public record NotificationItemDto
{
    public Guid Id { get; init; }
    public string TemplateKey { get; init; } = null!;
    public string Title { get; init; } = null!;
    public string Body { get; init; } = null!;
    public string? ActionUrl { get; init; }
    public bool IsRead { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

