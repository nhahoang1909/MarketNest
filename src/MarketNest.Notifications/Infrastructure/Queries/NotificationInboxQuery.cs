using MarketNest.Base.Common;
using MarketNest.Notifications.Application;
using MarketNest.Notifications.Domain;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Notifications.Infrastructure;

public class GetNotificationInboxQuery(NotificationsReadDbContext db) : IGetNotificationInboxQuery
{
    public async Task<PagedResult<NotificationItemDto>> ExecuteAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Notifications
            .Where(n => n.UserId == userId && n.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(n => n.IsRead ? 1 : 0) // unread first
            .ThenByDescending(n => n.CreatedAt);

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationItemDto
            {
                Id = n.Id,
                TemplateKey = n.TemplateKey,
                Title = n.Title,
                Body = n.Body,
                ActionUrl = n.ActionUrl,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(ct);

        return new PagedResult<NotificationItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }
}

public class UnreadCountQuery(NotificationsReadDbContext db) : IGetUnreadCountQuery
{
    public Task<int> ExecuteAsync(Guid userId, CancellationToken ct = default)
        => db.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead && n.ExpiresAt > DateTimeOffset.UtcNow, ct);
}

