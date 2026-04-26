using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Admin.Config;

public class CommissionModel(IAppLogger<CommissionModel> logger) : PageModel
{
    public void OnGet()
    {
        logger.Info("API {Api} Start - CorrelationId={Cid}", nameof(OnGet), HttpContext?.TraceIdentifier ?? "-");
    }

    public void OnPost()
    {
        logger.Info("API {Api} Start - CorrelationId={Cid}", nameof(OnPost), HttpContext?.TraceIdentifier ?? "-");
    }
}
