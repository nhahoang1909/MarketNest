using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Account.Disputes;

public partial class DetailModel(IAppLogger<DetailModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public Guid DisputeId { get; set; }

    public void OnGet()
        => Log.InfoOnGet(logger, DisputeId, HttpContext?.TraceIdentifier ?? "-");

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AccountDisputesDetailStart, LogLevel.Information,
            "AccountDisputeDetail OnGet Start - DisputeId={DisputeId} CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, Guid disputeId, string correlationId);
    }
}
