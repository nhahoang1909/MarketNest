using MarketNest.Notifications.Application;
using MarketNest.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Notifications.Infrastructure;

public class NotificationRepository(NotificationsDbContext db)
    : BaseRepository<Notification, Guid>(db), INotificationRepository
{
    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
        => Db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead && n.ExpiresAt > DateTimeOffset.UtcNow, ct);

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        await Db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(n => n.IsRead, true)
                .SetProperty(n => n.ReadAt, now), ct);
    }
}

