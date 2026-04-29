using MarketNest.Base.Infrastructure;
using MarketNest.Catalog.Domain;

namespace MarketNest.Catalog.Infrastructure;

/// <summary>
///     Module-local BaseRepository wired to <see cref="CatalogDbContext"/>.
///     All write-side infrastructure is provided by <see cref="BaseRepository{TEntity,TKey,TContext}"/>.
/// </summary>
public abstract class BaseRepository<TEntity, TKey>(CatalogDbContext db)
    : BaseRepository<TEntity, TKey, CatalogDbContext>(db)
    where TEntity : Entity<TKey>;

