using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Account.Orders;

public partial class ReviewModel(IAppLogger<ReviewModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid OrderId { get; set; }

    public void OnGet()
        => Log.InfoOnGet(logger, OrderId, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, OrderId, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AccountOrdersReviewStart, LogLevel.Information,
            "AccountOrderReview OnGet Start - OrderId={OrderId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, Guid orderId, string correlationId);

        [LoggerMessage((int)LogEventId.AccountOrdersReviewStart + 1, LogLevel.Information,
            "AccountOrderReview OnPost Start - OrderId={OrderId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, Guid orderId, string correlationId);
    }
}
