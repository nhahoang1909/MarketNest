using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Shop.Products;

public class DetailModel : PageModel
{
    [BindProperty(SupportsGet = true)] public string Slug { get; set; } = default!;

    [BindProperty(SupportsGet = true)] public Guid ProductId { get; set; }

    public void OnGet()
    {
    }
}
