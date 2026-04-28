using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Admin.Config;

public partial class NationalityModel(
    IReferenceDataReadService referenceData,
    IAppLogger<NationalityModel> logger) : PageModel
{
    public IReadOnlyList<NationalityDto> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");
        Items = await referenceData.GetNationalitiesAsync(ct);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminConfigNationalityStart, LogLevel.Information,
            "AdminConfig.Nationality OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}

