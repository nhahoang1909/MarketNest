using MediatR;

#pragma warning disable IDE0130
namespace MarketNest.Admin.Application;
#pragma warning restore IDE0130

public partial class UpdateAnnouncementHandler(
    IAnnouncementRepository repository,
    IAppLogger<UpdateAnnouncementHandler> logger) : ICommandHandler<UpdateAnnouncementCommand, Unit>
{
    public async Task<Result<Unit, Error>> Handle(UpdateAnnouncementCommand request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.Id);

        var entity = await repository.FindByKeyAsync(request.Id, cancellationToken);
        if (entity is null)
            return Result<Unit, Error>.Failure(Error.NotFound("Announcement", request.Id.ToString()));

        entity.Update(
            request.Title,
            request.Message,
            request.Type,
            request.StartDateUtc,
            request.EndDateUtc,
            request.IsDismissible,
            request.SortOrder,
            request.LinkUrl,
            request.LinkText);

        repository.Update(entity);

        Log.InfoSuccess(logger, request.Id);
        return Result<Unit, Error>.Success(Unit.Value);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminUpdateAnnouncementStart, LogLevel.Information,
            "UpdateAnnouncement Start - Id={Id}")]
        public static partial void InfoStart(ILogger logger, Guid id);

        [LoggerMessage((int)LogEventId.AdminUpdateAnnouncementSuccess, LogLevel.Information,
            "UpdateAnnouncement Success - Id={Id}")]
        public static partial void InfoSuccess(ILogger logger, Guid id);
    }
}

