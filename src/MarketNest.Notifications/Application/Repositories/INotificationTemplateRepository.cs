using MarketNest.Base.Infrastructure;
using MarketNest.Notifications.Domain;

namespace MarketNest.Notifications.Application;

/// <summary>Write-side repository for NotificationTemplate aggregate.</summary>
public interface INotificationTemplateRepository : IBaseRepository<NotificationTemplate, Guid>
{
    Task<NotificationTemplate?> GetByKeyAsync(string templateKey, CancellationToken ct = default);
}

