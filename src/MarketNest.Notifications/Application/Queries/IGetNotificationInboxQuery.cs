using MarketNest.Base.Common;

namespace MarketNest.Notifications.Application;

/// <summary>Query for user's notification inbox (paged, unread first).</summary>
public interface IGetNotificationInboxQuery
{
    Task<PagedResult<NotificationItemDto>> ExecuteAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default);
}

/// <summary>Query for unread notification count.</summary>
public interface IGetUnreadCountQuery
{
    Task<int> ExecuteAsync(Guid userId, CancellationToken ct = default);
}

