using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Reviews;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
        => Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerReviewsIndexStart, LogLevel.Information,
            "SellerReviews OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}
