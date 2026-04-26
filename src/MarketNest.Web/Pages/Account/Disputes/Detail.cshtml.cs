using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Account.Disputes;

public class DetailModel(IAppLogger<DetailModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid DisputeId { get; set; }

    public void OnGet()
    {
        logger.Info("API {Api} Start - CorrelationId={Cid} Payload={Payload}", nameof(OnGet), HttpContext?.TraceIdentifier ?? "-", new { DisputeId });
    }
}
