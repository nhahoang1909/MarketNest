using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Orders;

public partial class ConfirmationModel(IAppLogger<ConfirmationModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid OrderId { get; set; }

    public void OnGet()
        => Log.InfoOnGet(logger, OrderId, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.OrdersConfirmationStart, LogLevel.Information,
            "OrderConfirmation OnGet Start - OrderId={OrderId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, Guid orderId, string correlationId);
    }
}
