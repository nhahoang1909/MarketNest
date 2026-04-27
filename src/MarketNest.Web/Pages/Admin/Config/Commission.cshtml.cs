using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Admin.Config;

public partial class CommissionModel(IAppLogger<CommissionModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminConfigCommissionStart, LogLevel.Information,
            "AdminCommission OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);

        [LoggerMessage((int)LogEventId.AdminConfigCommissionStart + 1, LogLevel.Information,
            "AdminCommission OnPost Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, string correlationId);
    }
}
