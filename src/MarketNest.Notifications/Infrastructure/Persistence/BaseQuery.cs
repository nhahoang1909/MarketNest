using MarketNest.Base.Domain;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Notifications.Infrastructure;

/// <summary>
///     Module-local BaseQuery wired to <see cref="NotificationsReadDbContext"/>.
/// </summary>
public abstract class BaseQuery<TEntity, TKey>(NotificationsReadDbContext db)
    : BaseQuery<TEntity, TKey, NotificationsReadDbContext>(db)
    where TEntity : Entity<TKey>;

