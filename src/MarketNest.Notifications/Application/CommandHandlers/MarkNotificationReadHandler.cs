using MarketNest.Base.Common;
using MediatR;

namespace MarketNest.Notifications.Application;

public class MarkNotificationReadHandler(INotificationRepository notifications)
    : ICommandHandler<MarkNotificationReadCommand, Unit>
{
    public async Task<Result<Unit, Error>> Handle(
        MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await notifications.FindByKeyAsync(request.NotificationId, cancellationToken);
        if (notification is null)
            return Result.Failure<Unit>(Error.NotFound("Notification", request.NotificationId.ToString()));

        if (notification.UserId != request.UserId)
            return Result.Failure<Unit>(Error.Forbidden("Cannot mark another user's notification as read."));

        notification.MarkAsRead();
        notifications.Update(notification);
        return Result.Success();
    }
}

public class MarkAllNotificationsReadHandler(INotificationRepository notifications)
    : ICommandHandler<MarkAllNotificationsReadCommand, Unit>
{
    public async Task<Result<Unit, Error>> Handle(
        MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        await notifications.MarkAllAsReadAsync(request.UserId, cancellationToken);
        return Result.Success();
    }
}
