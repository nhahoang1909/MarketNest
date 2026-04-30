namespace MarketNest.Notifications.Application;

/// <summary>
///     Sends rendered email messages via SMTP (Phase 1) or transactional email provider (Phase 2+).
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>Fully rendered email ready for dispatch.</summary>
public record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null);

