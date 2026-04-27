namespace MarketNest.Base.Common;

/// <summary>
///     Implemented by Notifications module; consumed by all modules via domain event handlers.
/// </summary>
public interface INotificationService
{
    Task SendEmailAsync(string recipient, string subject, string htmlBody, CancellationToken ct = default);
    Task SendTemplatedEmailAsync(string recipient, string templateName, object model, CancellationToken ct = default);
}
