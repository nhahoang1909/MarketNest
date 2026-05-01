namespace MarketNest.Admin.Application;

public record GetActiveAnnouncementsQuery : IQuery<IReadOnlyList<ActiveAnnouncementDto>>;

