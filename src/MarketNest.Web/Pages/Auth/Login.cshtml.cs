using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Auth;

public class LoginModel(IAppLogger<LoginModel> logger) : PageModel
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
