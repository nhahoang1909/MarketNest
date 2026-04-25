using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages;

[Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryToken]
public class NotFoundModel : PageModel
{
    public void OnGet()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
    }
}

