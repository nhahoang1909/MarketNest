using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Products;

public partial class EditModel(IAppLogger<EditModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid ProductId { get; set; }

    public void OnGet()
        => Log.InfoOnGet(logger, ProductId, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, ProductId, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerProductsEditStart, LogLevel.Information,
            "SellerProductEdit OnGet Start - ProductId={ProductId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, Guid productId, string correlationId);

        [LoggerMessage((int)LogEventId.SellerProductsEditStart + 1, LogLevel.Information,
            "SellerProductEdit OnPost Start - ProductId={ProductId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, Guid productId, string correlationId);
    }
}
