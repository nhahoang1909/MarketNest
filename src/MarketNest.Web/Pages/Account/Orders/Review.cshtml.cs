using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Account.Orders;

public class ReviewModel(IAppLogger<ReviewModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid OrderId { get; set; }

    public void OnGet()
    {
        logger.Info("API {Api} Start - CorrelationId={Cid} Payload={Payload}", nameof(OnGet), HttpContext?.TraceIdentifier ?? "-", new { OrderId });
    }

    public void OnPost()
    {
        logger.Info("API {Api} Start - CorrelationId={Cid} Payload={Payload}", nameof(OnPost), HttpContext?.TraceIdentifier ?? "-", new { OrderId });
    }
}
