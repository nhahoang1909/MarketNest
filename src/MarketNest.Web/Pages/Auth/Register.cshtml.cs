using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Auth;

public partial class RegisterModel(IAppLogger<RegisterModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AuthRegisterStart, LogLevel.Information,
            "Register OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);

        [LoggerMessage((int)LogEventId.AuthRegisterStart + 1, LogLevel.Information,
            "Register OnPost Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, string correlationId);
    }
}
