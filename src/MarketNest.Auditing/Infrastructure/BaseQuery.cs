using MarketNest.Base.Domain;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Auditing.Infrastructure;

/// <summary>
///     Module-local BaseQuery wired to <see cref="AuditingReadDbContext"/>.
///     All read-side infrastructure is provided by <see cref="BaseQuery{TEntity,TKey,TContext}"/>.
/// </summary>
public abstract class BaseQuery<TEntity, TKey>(AuditingReadDbContext db)
    : BaseQuery<TEntity, TKey, AuditingReadDbContext>(db)
    where TEntity : Entity<TKey>;

