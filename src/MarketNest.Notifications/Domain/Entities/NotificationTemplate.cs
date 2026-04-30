using MarketNest.Base.Domain;

namespace MarketNest.Notifications.Domain;

/// <summary>
///     Admin-managed notification template. Contains the subject/body patterns
///     with Handlebars-style <c>{{Variable}}</c> placeholders.
///     <para>
///         TemplateKey is immutable after creation — code references it. Admin can only
///         edit SubjectTemplate, BodyTemplate, and IsActive.
///     </para>
/// </summary>
public class NotificationTemplate : AggregateRoot
{
#pragma warning disable CS8618 // Non-nullable field — EF Core uses this constructor
    private NotificationTemplate() { }
#pragma warning restore CS8618

    public NotificationTemplate(
        string templateKey,
        string displayName,
        NotificationChannel channel,
        string? subjectTemplate,
        string bodyTemplate,
        string[] availableVariables)
    {
        TemplateKey = templateKey;
        DisplayName = displayName;
        Channel = channel;
        SubjectTemplate = subjectTemplate;
        BodyTemplate = bodyTemplate;
        AvailableVariables = availableVariables;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Stable key referenced from code, e.g. "order.placed.buyer". Immutable after creation.</summary>
    public string TemplateKey { get; private set; }

    /// <summary>Admin UI label.</summary>
    public string DisplayName { get; private set; }

    /// <summary>Dispatch channel: Email, InApp, or Both.</summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>Email subject line template. Null for InApp-only templates.</summary>
    public string? SubjectTemplate { get; private set; }  // null = InApp-only, no email subject

    /// <summary>HTML body template with {{Variable}} placeholders.</summary>
    public string BodyTemplate { get; private set; }

    /// <summary>Documented list of valid variable names for this template.</summary>
    public string[] AvailableVariables { get; private set; } = [];

    /// <summary>When inactive, system uses hardcoded fallback and logs warning.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Admin who last modified this template.</summary>
    public Guid? LastModifiedBy { get; private set; }  // null = never modified after creation

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }  // null = never updated

    // ── Domain Methods ─────────────────────────────────────────────────────

    public void UpdateContent(string? subjectTemplate, string bodyTemplate, Guid modifiedBy)
    {
        SubjectTemplate = subjectTemplate;
        BodyTemplate = bodyTemplate;
        LastModifiedBy = modifiedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate(Guid modifiedBy)
    {
        IsActive = true;
        LastModifiedBy = modifiedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Deactivate(Guid modifiedBy)
    {
        IsActive = false;
        LastModifiedBy = modifiedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

