using MarketNest.Base.Domain;

namespace MarketNest.Notifications.Domain;

/// <summary>
///     In-app notification stored in the user's inbox.
///     Rendered at dispatch time. Expires after 90 days.
/// </summary>
public class Notification : Entity<Guid>
{
#pragma warning disable CS8618 // Non-nullable field — EF Core uses this constructor
    private Notification() { }
#pragma warning restore CS8618

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
    public string TemplateKey { get; private set; }
    public string Title { get; private set; }
    public string Body { get; private set; }
    public string? ActionUrl { get; private set; }   // null = no clickable action link
    public bool IsRead { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }  // null = not yet read
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

