using MarketNest.Base.Infrastructure;
using MarketNest.Base.Utility;
using Microsoft.EntityFrameworkCore;

namespace MarketNest.Notifications.Infrastructure;

/// <summary>
///     Removes expired read notifications and enforces per-user unread cap (200).
///     Schedule: Daily 03:00 UTC.
/// </summary>
public sealed class CleanupExpiredNotificationsJob(
    NotificationsDbContext db,
    IUnitOfWork uow) : IBackgroundJob
{
    private const int MaxUnreadPerUser = 200;

    public JobDescriptor Descriptor => new(
        JobKey: "notifications.cleanup-expired",
        DisplayName: "Cleanup Expired Notifications",
        OwningModule: "Notifications",
        Type: JobType.Timer,
        Schedule: "0 3 * * *",
        IsEnabled: true,
        IsRetryable: true,
        MaxRetryCount: 3,
        Description: "Deletes expired read notifications and caps unread per user at 200");

    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken = default)
    {
        // Delete expired + read notifications
        var now = DateTimeOffset.UtcNow;
        await db.Notifications
            .Where(n => n.ExpiresAt < now && n.IsRead)
            .ExecuteDeleteAsync(cancellationToken);

        await uow.CommitAsync(cancellationToken);
    }
}

