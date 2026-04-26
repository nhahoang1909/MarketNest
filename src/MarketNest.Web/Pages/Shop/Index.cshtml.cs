using Microsoft.AspNetCore.Mvc.RazorPages;
namespace MarketNest.Web.Pages.Shop;

public class IndexModel(IAppLogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Slug { get; set; } = default!;

    public void OnGet()
    {
        var cid = HttpContext?.TraceIdentifier ?? "-";
        using var scope = logger.BeginApiScope(nameof(OnGet), null, cid);
        scope.Success();
    }
}
