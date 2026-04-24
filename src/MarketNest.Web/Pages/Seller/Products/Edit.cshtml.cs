using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Seller.Products;

public class EditModel : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid ProductId { get; set; }

    public void OnGet()
    {
    }

    public void OnPost()
    {
    }
}
