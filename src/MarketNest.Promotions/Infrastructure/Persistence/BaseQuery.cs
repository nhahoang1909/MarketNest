using System.Linq.Expressions;
using MarketNest.Base.Infrastructure;

namespace MarketNest.Promotions.Infrastructure;

/// <summary>
///     Module-local BaseQuery wired to <see cref="PromotionsReadDbContext"/>.
///     All read-side infrastructure is provided by <see cref="BaseQuery{TEntity,TKey,TContext}"/>.
/// </summary>
public abstract class BaseQuery<TEntity, TKey>(PromotionsReadDbContext db)
    : BaseQuery<TEntity, TKey, PromotionsReadDbContext>(db)
    where TEntity : Entity<TKey>;
