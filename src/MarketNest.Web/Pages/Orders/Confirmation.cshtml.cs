using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Orders;

public class ConfirmationModel(IAppLogger<ConfirmationModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid OrderId { get; set; }

    public void OnGet()
    {
        var cid = HttpContext?.TraceIdentifier ?? "-";
        using var scope = logger.BeginApiScope(nameof(OnGet), new { OrderId }, cid);
        scope.Success();
    }
}
