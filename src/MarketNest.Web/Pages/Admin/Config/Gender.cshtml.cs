using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Admin.Config;

public partial class GenderModel(
    IReferenceDataReadService referenceData,
    IAppLogger<GenderModel> logger) : PageModel
{
    public IReadOnlyList<GenderDto> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");
        Items = await referenceData.GetGendersAsync(ct);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminConfigGenderStart, LogLevel.Information,
            "AdminConfig.Gender OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}

