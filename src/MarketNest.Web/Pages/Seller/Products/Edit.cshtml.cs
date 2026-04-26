using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Products;

public class EditModel(IAppLogger<EditModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid ProductId { get; set; }

    public void OnGet()
    {
        var cid = HttpContext?.TraceIdentifier ?? "-";
        using var scope = logger.BeginApiScope(nameof(OnGet), new { ProductId }, cid);
        scope.Success();
    }

    public void OnPost()
    {
        var cid = HttpContext?.TraceIdentifier ?? "-";
        using var scope = logger.BeginApiScope(nameof(OnPost), new { ProductId }, cid);
        scope.Success();
    }
}
