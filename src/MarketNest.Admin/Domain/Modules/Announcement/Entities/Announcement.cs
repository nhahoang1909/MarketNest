namespace MarketNest.Admin.Domain;

/// <summary>
///     Admin-managed announcement displayed on the public site (navbar banner, hero section).
///     Supports scheduling (start/end dates) and type-based styling.
/// </summary>
public class Announcement : Entity<Guid>
{
#pragma warning disable CS8618 // Non-nullable field — EF Core uses this constructor
    protected Announcement()
    {
    }
#pragma warning restore CS8618

    public Announcement(
        Guid id,
        string title,
        string message,
        AnnouncementType type,
        DateTimeOffset startDateUtc,
        DateTimeOffset endDateUtc,
        bool isDismissible,
        int sortOrder,
        string? linkUrl = null,
        string? linkText = null)
    {
        Id = id;
        Title = title;
        Message = message;
        Type = type;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        IsDismissible = isDismissible;
        SortOrder = sortOrder;
        LinkUrl = linkUrl;
        LinkText = linkText;
        IsPublished = false;
    }

    public string Title { get; private set; }
    public string Message { get; private set; }
    public AnnouncementType Type { get; private set; }

    /// <summary>Optional CTA link URL.</summary>
    public string? LinkUrl { get; private set; }

    /// <summary>Optional CTA link display text.</summary>
    public string? LinkText { get; private set; }

    public DateTimeOffset StartDateUtc { get; private set; }
    public DateTimeOffset EndDateUtc { get; private set; }
    public bool IsPublished { get; private set; }
    public bool IsDismissible { get; private set; }
    public int SortOrder { get; private set; }

    // ── Domain methods ──────────────────────────────────────────────────

    public void Publish() => IsPublished = true;

    public void Unpublish() => IsPublished = false;

    public bool IsActive(DateTimeOffset utcNow)
        => IsPublished && StartDateUtc <= utcNow && EndDateUtc > utcNow;

    public void Update(
        string title,
        string message,
        AnnouncementType type,
        DateTimeOffset startDateUtc,
        DateTimeOffset endDateUtc,
        bool isDismissible,
        int sortOrder,
        string? linkUrl,
        string? linkText)
    {
        Title = title;
        Message = message;
        Type = type;
        StartDateUtc = startDateUtc;
        EndDateUtc = endDateUtc;
        IsDismissible = isDismissible;
        SortOrder = sortOrder;
        LinkUrl = linkUrl;
        LinkText = linkText;
    }
}

