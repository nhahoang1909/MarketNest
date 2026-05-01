namespace MarketNest.Admin.Application;

public record GetAnnouncementsPagedQuery : PagedQuery, IQuery<PagedResult<AnnouncementDto>>
{
    public string? SearchTitle { get; init; }
}

