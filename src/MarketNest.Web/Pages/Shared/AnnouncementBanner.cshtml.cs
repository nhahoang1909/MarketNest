using MarketNest.Admin.Application;
using MediatR;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace MarketNest.Web.Pages.Shared;

public class AnnouncementBannerModel(ISender sender) : PageModel
{
    public IReadOnlyList<ActiveAnnouncementDto> Announcements { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        Announcements = await sender.Send(new GetActiveAnnouncementsQuery(), ct);
    }
}
