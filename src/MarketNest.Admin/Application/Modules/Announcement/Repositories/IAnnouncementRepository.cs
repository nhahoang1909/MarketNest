using MarketNest.Admin.Domain;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Admin.Application;

public interface IAnnouncementRepository : IBaseRepository<Announcement, Guid>
{
}

