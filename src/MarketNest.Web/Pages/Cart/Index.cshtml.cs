using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Cart;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.CartIndexStart, LogLevel.Information,
            "Cart OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
