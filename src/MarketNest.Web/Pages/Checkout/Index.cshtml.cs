using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Checkout;

public class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
    {
        // minimal trace log to satisfy logging requirement
        logger.Info("API {Api} Start - CorrelationId={Cid}", nameof(OnGet), HttpContext?.TraceIdentifier ?? "-");
    }
}
