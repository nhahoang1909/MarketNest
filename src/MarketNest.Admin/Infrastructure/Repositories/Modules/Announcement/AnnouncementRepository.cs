using MarketNest.Admin.Application;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

public class AnnouncementRepository(AdminDbContext db)
    : BaseRepository<Announcement, Guid>(db), IAnnouncementRepository
{
}

