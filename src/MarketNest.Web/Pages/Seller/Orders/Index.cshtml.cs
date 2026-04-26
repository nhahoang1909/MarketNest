using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Orders;

public class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    public void OnGet()
    {
        var cid = HttpContext?.TraceIdentifier ?? "-";
        using var scope = logger.BeginApiScope(nameof(OnGet), null, cid);
        scope.Success();
    }
}
