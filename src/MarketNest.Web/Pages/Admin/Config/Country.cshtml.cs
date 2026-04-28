using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Admin.Config;

public partial class CountryModel(
    IReferenceDataReadService referenceData,
    IAppLogger<CountryModel> logger) : PageModel
{
    public IReadOnlyList<CountryDto> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");
        Items = await referenceData.GetCountriesAsync(ct);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminConfigCountryStart, LogLevel.Information,
            "AdminConfig.Country OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}

