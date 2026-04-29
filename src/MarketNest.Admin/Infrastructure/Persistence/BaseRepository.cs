using MarketNest.Base.Infrastructure;

namespace MarketNest.Admin.Infrastructure;

/// <summary>
///     Module-local BaseRepository wired to <see cref="AdminDbContext"/>.
///     All write-side infrastructure is provided by <see cref="BaseRepository{TEntity,TKey,TContext}"/>.
/// </summary>
public abstract class BaseRepository<TEntity, TKey>(AdminDbContext db)
    : BaseRepository<TEntity, TKey, AdminDbContext>(db)
    where TEntity : Entity<TKey>;
