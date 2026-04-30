namespace MarketNest.Base.Common;

/// <summary>
///     Implemented by Notifications module; consumed by all modules via domain event handlers.
///     Dispatches both email and in-app notifications using admin-managed templates.
/// </summary>
public interface INotificationService
{
    /// <summary>
    ///     Template-based dispatch — sends email and/or in-app notification per template config.
    /// </summary>
    Task SendAsync(
        Guid recipientUserId,
        string templateKey,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken ct = default);

    /// <summary>
    ///     Sends to multiple recipients simultaneously (e.g., order.placed → buyer + seller).
    /// </summary>
    Task SendToMultipleAsync(
        IEnumerable<Guid> recipientUserIds,
        string templateKey,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken ct = default);

    /// <summary>
    ///     Security emails — always sent, never checks user preference toggles.
    /// </summary>
    Task SendSecurityEmailAsync(
        string toEmail,
        string templateKey,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken ct = default);
}
