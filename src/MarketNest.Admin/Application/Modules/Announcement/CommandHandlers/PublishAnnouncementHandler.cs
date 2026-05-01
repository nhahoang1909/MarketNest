using MediatR;

#pragma warning disable IDE0130
namespace MarketNest.Admin.Application;
#pragma warning restore IDE0130

public partial class PublishAnnouncementCommandHandler(
    IAnnouncementRepository repository,
    IAppLogger<PublishAnnouncementCommandHandler> logger) : ICommandHandler<PublishAnnouncementCommand, Unit>
{
    public async Task<Result<Unit, Error>> Handle(PublishAnnouncementCommand request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.Id, request.Publish);

        var entity = await repository.FindByKeyAsync(request.Id, cancellationToken);
        if (entity is null)
            return Result<Unit, Error>.Failure(Error.NotFound("Announcement", request.Id.ToString()));

        if (request.Publish)
            entity.Publish();
        else
            entity.Unpublish();

        repository.Update(entity);

        Log.InfoSuccess(logger, request.Id, request.Publish);
        return Result<Unit, Error>.Success(Unit.Value);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminPublishAnnouncementStart, LogLevel.Information,
            "PublishAnnouncement Start - Id={Id}, Publish={Publish}")]
        public static partial void InfoStart(ILogger logger, Guid id, bool publish);

        [LoggerMessage((int)LogEventId.AdminPublishAnnouncementSuccess, LogLevel.Information,
            "PublishAnnouncement Success - Id={Id}, Published={Published}")]
        public static partial void InfoSuccess(ILogger logger, Guid id, bool published);
    }
}

