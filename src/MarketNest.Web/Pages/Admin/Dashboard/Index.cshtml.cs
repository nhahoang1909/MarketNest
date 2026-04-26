using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Admin.Dashboard;

public class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
    {
        logger.Info("API {Api} Start - CorrelationId={Cid}", nameof(OnGet), HttpContext?.TraceIdentifier ?? "-");
    }
}
