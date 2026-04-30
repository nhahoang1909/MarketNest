using MarketNest.Notifications.Application;
using MarketNest.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Notifications.Infrastructure;

public class NotificationTemplateRepository(NotificationsDbContext db)
    : BaseRepository<NotificationTemplate, Guid>(db), INotificationTemplateRepository
{
    public Task<NotificationTemplate?> GetByKeyAsync(string templateKey, CancellationToken ct = default)
        => Db.NotificationTemplates.FirstOrDefaultAsync(t => t.TemplateKey == templateKey, ct);
}

