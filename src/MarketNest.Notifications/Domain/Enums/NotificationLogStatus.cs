namespace MarketNest.Notifications.Domain;

/// <summary>
///     Status of a dispatched notification log entry.
/// </summary>
public enum NotificationLogStatus
{
    Sent = 1,
    Failed = 2,
    Skipped = 3
}

