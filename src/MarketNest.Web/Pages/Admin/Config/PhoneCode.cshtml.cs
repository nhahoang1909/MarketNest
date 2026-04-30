using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Admin.Config;

public partial class PhoneCodeModel(
    IReferenceDataReadService referenceData,
    IAppLogger<PhoneCodeModel> logger) : PageModel
{
    public IReadOnlyList<PhoneCountryCodeDto> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Log.InfoOnGet(logger, HttpContext?.TraceIdentifier ?? "-");
        Items = await referenceData.GetPhoneCountryCodesAsync(ct);
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AdminConfigPhoneCodeStart, LogLevel.Information,
            "AdminConfig.PhoneCode OnGet Start - CorrelationId={CorrelationId}")]
        public static partial void InfoOnGet(ILogger logger, string correlationId);
    }
}

