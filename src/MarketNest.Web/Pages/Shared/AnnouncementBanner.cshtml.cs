using MarketNest.Admin.Application;
using MarketNest.Base.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Shared;

public partial class AnnouncementBannerModel(ISender sender, IAppLogger<AnnouncementBannerModel> logger) : PageModel
{
    public IReadOnlyList<ActiveAnnouncementDto> Announcements { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Log.FetchingActiveAnnouncements(logger);
        Announcements = await sender.Send(new GetActiveAnnouncementsQuery(), ct);
    }

    private static partial class Log
    {
        [Microsoft.Extensions.Logging.LoggerMessage(
            EventId = 180001,
            Level = Microsoft.Extensions.Logging.LogLevel.Debug,
            Message = "Fetching active announcements")]
        public static partial void FetchingActiveAnnouncements(IAppLogger<AnnouncementBannerModel> logger);
    }
}
