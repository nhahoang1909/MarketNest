using MarketNest.Base.Infrastructure;

namespace MarketNest.Promotions.Infrastructure;

/// <summary>
///     Module-local BaseRepository wired to <see cref="PromotionsDbContext"/>.
///     All write-side infrastructure is provided by <see cref="BaseRepository{TEntity,TKey,TContext}"/>.
/// </summary>
public abstract class BaseRepository<TEntity, TKey>(PromotionsDbContext db)
    : BaseRepository<TEntity, TKey, PromotionsDbContext>(db)
    where TEntity : Entity<TKey>;
