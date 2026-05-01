using MarketNest.Admin.Domain;

#pragma warning disable IDE0130
namespace MarketNest.Admin.Application;
#pragma warning restore IDE0130

public partial class CreateAnnouncementHandler(
    IAnnouncementRepository repository,
    IAppLogger<CreateAnnouncementHandler> logger) : ICommandHandler<CreateAnnouncementCommand, Guid>
{
    public Task<Result<Guid, Error>> Handle(CreateAnnouncementCommand request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.Title);

        var id = Guid.NewGuid();
        var entity = new Announcement(
            id,
            request.Title,
            request.Message,
            request.Type,
            request.StartDateUtc,
            request.EndDateUtc,
            request.IsDismissible,
            request.SortOrder,
            request.LinkUrl,
            request.LinkText);

        repository.Add(entity);

        Log.InfoSuccess(logger, id);
        return Task.FromResult(Result<Guid, Error>.Success(id));
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminCreateAnnouncementStart, LogLevel.Information,
            "CreateAnnouncement Start - Title={Title}")]
        public static partial void InfoStart(ILogger logger, string title);

        [LoggerMessage((int)LogEventId.AdminCreateAnnouncementSuccess, LogLevel.Information,
            "CreateAnnouncement Success - Id={Id}")]
        public static partial void InfoSuccess(ILogger logger, Guid id);
    }
}

