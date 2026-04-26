using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages;

[IgnoreAntiforgeryToken]
public class NotFoundModel(IAppLogger<NotFoundModel> logger) : PageModel
{
    public void OnGet()
    {
        var cid = HttpContext?.TraceIdentifier ?? "-";
        using var scope = logger.BeginApiScope(nameof(OnGet), null, cid);
        scope.Success();
        Response.StatusCode = StatusCodes.Status404NotFound;
    }
}
