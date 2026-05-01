#pragma warning disable IDE0130
namespace MarketNest.Admin.Application;
#pragma warning restore IDE0130

public partial class GetActiveAnnouncementsQueryHandler(
    IGetActiveAnnouncementsQuery query,
    IAppLogger<GetActiveAnnouncementsQueryHandler> logger) : IQueryHandler<GetActiveAnnouncementsQuery, IReadOnlyList<ActiveAnnouncementDto>>
{
    public Task<IReadOnlyList<ActiveAnnouncementDto>> Handle(
        GetActiveAnnouncementsQuery request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger);
        return query.ExecuteAsync(cancellationToken);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminGetActiveAnnouncementsStart, LogLevel.Information,
            "GetActiveAnnouncements Start")]
        public static partial void InfoStart(ILogger logger);
    }
}
