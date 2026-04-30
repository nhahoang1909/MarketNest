using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Admin.Config;

public partial class ProductCategoryModel(
    IReferenceDataReadService referenceData,
    IAppLogger<ProductCategoryModel> logger) : PageModel
{
    public IReadOnlyList<ProductCategoryDto> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");
        Items = await referenceData.GetProductCategoriesAsync(ct);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminConfigProductCategoryStart, LogLevel.Information,
            "AdminConfig.ProductCategory OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}

