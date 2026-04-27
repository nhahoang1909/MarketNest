using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Auth;

public partial class LoginModel(IAppLogger<LoginModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AuthLoginStart, LogLevel.Information,
            "Login OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);

        [LoggerMessage((int)LogEventId.AuthLoginStart + 1, LogLevel.Information,
            "Login OnPost Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, string correlationId);
    }
}
