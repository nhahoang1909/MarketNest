using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Orders;

public partial class DetailModel(IAppLogger<DetailModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid OrderId { get; set; }

    public void OnGet()
        => Log.InfoOnGet(logger, OrderId, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerOrdersDetailStart, LogLevel.Information,
            "SellerOrderDetail OnGet Start - OrderId={OrderId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, Guid orderId, string correlationId);
    }
}
