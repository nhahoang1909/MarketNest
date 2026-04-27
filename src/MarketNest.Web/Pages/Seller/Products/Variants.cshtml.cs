using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Products;

public partial class VariantsModel(IAppLogger<VariantsModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid ProductId { get; set; }

    public void OnGet()
        => Log.InfoOnGet(logger, ProductId, HttpContext?.TraceIdentifier ?? "-");

    public void OnPost()
        => Log.InfoOnPost(logger, ProductId, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.SellerProductsVariantsStart, LogLevel.Information,
            "SellerProductVariants OnGet Start - ProductId={ProductId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, Guid productId, string correlationId);

        [LoggerMessage((int)LogEventId.SellerProductsVariantsStart + 1, LogLevel.Information,
            "SellerProductVariants OnPost Start - ProductId={ProductId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnPost(ILogger logger, Guid productId, string correlationId);
    }
}
