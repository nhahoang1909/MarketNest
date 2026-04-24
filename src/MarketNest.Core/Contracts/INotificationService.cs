namespace MarketNest.Core.Contracts;

/// <summary>
/// Implemented by Notifications module; consumed by all modules via domain event handlers.
/// </summary>
public interface INotificationService
{
    Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    Task SendTemplatedEmailAsync(string to, string templateName, object model, CancellationToken ct = default);
}
