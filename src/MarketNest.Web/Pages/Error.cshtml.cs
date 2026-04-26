using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel(IAppLogger<ErrorModel> logger) : PageModel
{
    public string? RequestId { get; set; }

    public string Timestamp { get; set; } = string.Empty;

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        Timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC";
        logger.Info("API {Api} Start - CorrelationId={Cid}", nameof(OnGet), HttpContext?.TraceIdentifier ?? "-");
    }
}
