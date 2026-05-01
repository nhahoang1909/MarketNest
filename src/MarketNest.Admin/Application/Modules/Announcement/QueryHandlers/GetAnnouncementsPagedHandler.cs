#pragma warning disable IDE0130
namespace MarketNest.Admin.Application;
#pragma warning restore IDE0130

public partial class GetAnnouncementsPagedQueryHandler(
    IGetAnnouncementsPagedQuery query,
    IAppLogger<GetAnnouncementsPagedQueryHandler> logger) : IQueryHandler<GetAnnouncementsPagedQuery, PagedResult<AnnouncementDto>>
{
    public Task<PagedResult<AnnouncementDto>> Handle(
        GetAnnouncementsPagedQuery request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.Page, request.PageSize);
        return query.ExecuteAsync(request, cancellationToken);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminGetAnnouncementsPagedStart, LogLevel.Information,
            "GetAnnouncementsPaged Start - Page={Page}, PageSize={PageSize}")]
        public static partial void InfoStart(ILogger logger, int page, int pageSize);
    }
}
