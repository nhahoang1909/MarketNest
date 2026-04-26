using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Auth;

public partial class ForgotPasswordModel(IAppLogger<ForgotPasswordModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AuthForgotPasswordStart, LogLevel.Information,
            "ForgotPassword OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);

        [LoggerMessage((int)LogEventId.AuthForgotPasswordStart + 1, LogLevel.Information,
            "ForgotPassword OnPost Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, string correlationId);
    }
}
