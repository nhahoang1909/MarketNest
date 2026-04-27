using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Account.Settings;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AccountSettingsStart, LogLevel.Information,
            "AccountSettings OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);

        [LoggerMessage((int)LogEventId.AccountSettingsStart + 1, LogLevel.Information,
            "AccountSettings OnPost Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, string correlationId);
    }
}
