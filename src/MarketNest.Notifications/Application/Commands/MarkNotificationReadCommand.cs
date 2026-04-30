using MarketNest.Base.Common;

namespace MarketNest.Notifications.Application;

/// <summary>Marks a single notification as read.</summary>
public record MarkNotificationReadCommand(Guid NotificationId, Guid UserId) : ICommand;

/// <summary>Marks all unread notifications as read for a user.</summary>
public record MarkAllNotificationsReadCommand(Guid UserId) : ICommand;

