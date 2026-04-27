using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public partial class ErrorModel(IAppLogger<ErrorModel> logger) : PageModel
{
    public string? RequestId { get; set; }
    public string Timestamp { get; set; } = string.Empty;

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        Timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";
        Log.InfoDisplayed(logger, RequestId);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.GlobalErrorDisplayed, LogLevel.Information,
            "Error page displayed - RequestId={RequestId}")]
        public static partial void InfoDisplayed(ILogger logger, string? requestId);
    }
}
