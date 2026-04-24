using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Account.Orders;

public class ReviewModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid OrderId { get; set; }

    public void OnGet()
    {
    }

    public void OnPost()
    {
    }
}
