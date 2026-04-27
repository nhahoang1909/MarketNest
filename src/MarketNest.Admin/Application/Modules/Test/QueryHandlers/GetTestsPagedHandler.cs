using MarketNest.Base.Common;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Admin.Application;

public partial class GetTestsPagedHandler(
    IGetTestsPagedQuery query,
    IAppLogger<GetTestsPagedHandler> logger) : IQueryHandler<GetTestsPagedQuery, PagedResult<TestDto>>
{
    public Task<PagedResult<TestDto>> Handle(GetTestsPagedQuery request, CancellationToken cancellationToken)
    {
        Log.InfoStart(logger, request.Page, request.PageSize);
        return query.ExecuteAsync(request, cancellationToken);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminGetTestsPagedStart, LogLevel.Information,
            "GetTestsPaged Start - Page={Page} PageSize={PageSize}")]
        public static partial void InfoStart(ILogger logger, int page, int pageSize);
    }
}
