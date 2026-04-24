using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Account.Disputes;

public class DetailModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public Guid DisputeId { get; set; }

    public void OnGet()
    {
    }
}
