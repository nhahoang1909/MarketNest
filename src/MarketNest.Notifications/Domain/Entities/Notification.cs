using MarketNest.Base.Domain;

namespace MarketNest.Notifications.Domain;

/// <summary>
///     In-app notification stored in the user's inbox.
///     Rendered at dispatch time. Expires after 90 days.
/// </summary>
public class Notification : Entity<Guid>
{
    private Notification() { } // EF Core

    public Notification(
        Guid userId,
        string templateKey,
        string title,
        string body,
        string? actionUrl)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        TemplateKey = templateKey;
        Title = title;
        Body = body;
        ActionUrl = actionUrl;
        IsRead = false;
        CreatedAt = DateTimeOffset.UtcNow;
        ExpiresAt = CreatedAt.AddDays(DefaultExpirationDays);
    }

    private const int DefaultExpirationDays = 90;
    private const int MaxTitleLength = 120;
    private const int MaxBodyLength = 500;

    public Guid UserId { get; private set; }
    public string TemplateKey { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public string? ActionUrl { get; private set; }
    public bool IsRead { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    // ── Domain Methods ─────────────────────────────────────────────────────

    public void MarkAsRead()
    {
        if (IsRead) return;
        IsRead = true;
        ReadAt = DateTimeOffset.UtcNow;
    }
}

