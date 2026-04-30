using System.Linq.Expressions;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Admin.Infrastructure;

/// <summary>
///     Module-local BaseQuery wired to <see cref="AdminReadDbContext"/>.
///     All read-side infrastructure is provided by <see cref="BaseQuery{TEntity,TKey,TContext}"/>.
/// </summary>
public abstract class BaseQuery<TEntity, TKey>(AdminReadDbContext db)
    : BaseQuery<TEntity, TKey, AdminReadDbContext>(db)
    where TEntity : Entity<TKey>;
