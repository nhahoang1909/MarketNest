using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Shop;

public class IndexModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Slug { get; set; } = default!;

    public void OnGet()
    {
    }
}
