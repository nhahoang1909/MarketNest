using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Products;

public class CreateModel(IAppLogger<CreateModel> logger) : PageModel
{
    public void OnGet()
    {
        var cid = HttpContext?.TraceIdentifier ?? "-";
        using var scope = logger.BeginApiScope(nameof(OnGet), null, cid);
        scope.Success();
    }

    public void OnPost()
    {
        var cid = HttpContext?.TraceIdentifier ?? "-";
        using var scope = logger.BeginApiScope(nameof(OnPost), null, cid);
        scope.Success();
    }
}
