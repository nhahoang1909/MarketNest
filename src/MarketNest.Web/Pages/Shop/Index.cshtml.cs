using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Shop;

public partial class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Slug { get; set; } = default!;

    public void OnGet()
        => Log.InfoOnGet(logger, Slug, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.ShopIndexStart, LogLevel.Information,
            "ShopIndex OnGet Start - Slug={Slug} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string slug, string correlationId);
    }
}
