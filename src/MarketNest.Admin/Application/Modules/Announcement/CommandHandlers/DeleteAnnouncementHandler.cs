using MediatR;

#pragma warning disable IDE0130
namespace MarketNest.Admin.Application;
#pragma warning restore IDE0130

public partial class DeleteAnnouncementCommandHandler(
    IAnnouncementRepository repository,
    IAppLogger<DeleteAnnouncementCommandHandler> logger) : ICommandHandler<DeleteAnnouncementCommand, Unit>
{
    public async Task<Result<Unit, Error>> Handle(DeleteAnnouncementCommand request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.Id);

        var entity = await repository.FindByKeyAsync(request.Id, cancellationToken);
        if (entity is null)
            return Result<Unit, Error>.Failure(Error.NotFound("Announcement", request.Id.ToString()));

        repository.Remove(entity);

        Log.InfoSuccess(logger, request.Id);
        return Result<Unit, Error>.Success(Unit.Value);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminDeleteAnnouncementStart, LogLevel.Information,
            "DeleteAnnouncement Start - Id={Id}")]
        public static partial void InfoStart(ILogger logger, Guid id);

        [LoggerMessage((int)LogEventId.AdminDeleteAnnouncementSuccess, LogLevel.Information,
            "DeleteAnnouncement Success - Id={Id}")]
        public static partial void InfoSuccess(ILogger logger, Guid id);
    }
}

