using MarketNest.Base.Domain;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Notifications.Infrastructure;

/// <summary>
///     Module-local BaseRepository wired to <see cref="NotificationsDbContext"/>.
/// </summary>
public abstract class BaseRepository<TEntity, TKey>(NotificationsDbContext db)
    : BaseRepository<TEntity, TKey, NotificationsDbContext>(db)
    where TEntity : Entity<TKey>;

