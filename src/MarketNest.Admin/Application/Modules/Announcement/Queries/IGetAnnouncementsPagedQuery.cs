namespace MarketNest.Admin.Application;

public interface IGetAnnouncementsPagedQuery
{
    Task<PagedResult<AnnouncementDto>> ExecuteAsync(GetAnnouncementsPagedQuery request, CancellationToken ct);
}

