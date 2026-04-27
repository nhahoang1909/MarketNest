using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Admin.Dashboard;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminDashboardStart, LogLevel.Information,
            "AdminDashboard OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
