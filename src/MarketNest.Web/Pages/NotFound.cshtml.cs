using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages;

[IgnoreAntiforgeryToken]
public partial class NotFoundModel(IAppLogger<NotFoundModel> logger) : PageModel
{
    public void OnGet()
    {
        Log.InfoDisplayed(logger, HttpContext?.TraceIdentifier ?? "-");
        Response.StatusCode = StatusCodes.Status404NotFound;
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.GlobalNotFoundDisplayed, LogLevel.Information,
            "NotFound page displayed - CorrelationId={CorrelationId}")]
        public static partial void InfoDisplayed(ILogger logger, string correlationId);
    }
}
