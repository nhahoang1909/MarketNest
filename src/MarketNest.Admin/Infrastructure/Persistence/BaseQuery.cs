using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MarketNest.Core.Common;
using MarketNest.Core.Common.Queries;

namespace MarketNest.Admin.Infrastructure;

public abstract class BaseQuery<TEntity, TKey>(AdminReadDbContext db)
    : IBaseQuery<TEntity, TKey>
    where TEntity : Entity<TKey>
{
    protected AdminReadDbContext Db => db;

    public virtual Task<TEntity?> GetByKeyAsync(TKey id, CancellationToken ct = default)
        => db.Set<TEntity>().FirstOrDefaultAsync(e => e.Id!.Equals(id), ct);

    public virtual async Task<TEntity> GetByKeyOrThrowAsync(TKey id, CancellationToken ct = default)
        => await GetByKeyAsync(id, ct)
           ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} '{id}' not found");

    public virtual Task<bool> ExistsAsync(TKey id, CancellationToken ct = default)
        => db.Set<TEntity>().AnyAsync(e => e.Id!.Equals(id), ct);

    public virtual async Task<IReadOnlyList<TEntity>> ListAsync(CancellationToken ct = default)
        => await db.Set<TEntity>().ToListAsync(ct);

    public virtual Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        => db.Set<TEntity>().FirstOrDefaultAsync(predicate, ct);

    public virtual Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default)
        => predicate is null
            ? db.Set<TEntity>().CountAsync(ct)
            : db.Set<TEntity>().CountAsync(predicate, ct);
}
