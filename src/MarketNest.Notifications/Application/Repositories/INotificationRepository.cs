using MarketNest.Base.Infrastructure;
using MarketNest.Notifications.Domain;

namespace MarketNest.Notifications.Application;

/// <summary>Write-side repository for Notification entity (in-app inbox).</summary>
public interface INotificationRepository : IBaseRepository<Notification, Guid>
{
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
}

