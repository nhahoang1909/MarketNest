namespace MarketNest.Admin.Application;

public interface IGetActiveAnnouncementsQuery
{
    Task<IReadOnlyList<ActiveAnnouncementDto>> ExecuteAsync(CancellationToken ct);
}

