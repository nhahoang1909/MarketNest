namespace MarketNest.Notifications.Domain;

/// <summary>
///     Determines which channel(s) a notification template dispatches to.
/// </summary>
public enum NotificationChannel
{
    Email = 1,
    InApp = 2,
    Both = 3
}

