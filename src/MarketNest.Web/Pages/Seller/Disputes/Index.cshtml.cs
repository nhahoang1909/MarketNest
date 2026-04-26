using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Disputes;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerDisputesIndexStart, LogLevel.Information,
            "SellerDisputes OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
