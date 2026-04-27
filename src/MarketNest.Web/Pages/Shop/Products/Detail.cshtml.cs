using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Shop.Products;

public partial class DetailModel(IAppLogger<DetailModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Slug { get; set; } = default!;
    [BindProperty(SupportsGet = true)] public Guid ProductId { get; set; }

    public void OnGet()
        => Log.InfoOnGet(logger, ProductId, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.ShopProductDetailStart, LogLevel.Information,
            "ShopProductDetail OnGet Start - ProductId={ProductId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, Guid productId, string correlationId);
    }
}
