using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Shop.Products;

public class DetailModel(IAppLogger<DetailModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Slug { get; set; } = default!;

    [BindProperty(SupportsGet = true)] public Guid ProductId { get; set; }

    public void OnGet()
    {
        var cid = HttpContext?.TraceIdentifier ?? "-";
        using var scope = logger.BeginApiScope(nameof(OnGet), new { ProductId }, cid);
        scope.Success();
    }
}
