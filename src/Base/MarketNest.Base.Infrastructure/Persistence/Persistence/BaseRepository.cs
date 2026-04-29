using System.Linq.Expressions;
using MarketNest.Base.Common;
using MarketNest.Base.Domain;

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     Shared abstract base for write-side repository classes.
///     Inherit in each module passing the module's write <see cref="DbContext"/> type.
///     <br/>
///     DbContext access is protected — child classes use inherited query methods instead of direct DB access.
///     All changes must be committed via <c>IUnitOfWork.CommitAsync</c>.
/// </summary>
/// <example>
///     <code>
///     public abstract class BaseRepository&lt;TEntity, TKey&gt;(CatalogDbContext db)
///         : BaseRepository&lt;TEntity, TKey, CatalogDbContext&gt;(db);
///     </code>
/// </example>
public abstract class BaseRepository<TEntity, TKey, TContext>(TContext db)
    : IBaseRepository<TEntity, TKey>
    where TEntity : Entity<TKey>
    where TContext : DbContext
{
    protected TContext Db => db;

    // ── Read ──────────────────────────────────────────────────────────────────

    public virtual async Task<TEntity> GetByKeyAsync(TKey id, CancellationToken ct = default)
        => await FindByKeyAsync(id, ct)
           ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} '{id}' not found.");

    public virtual Task<TEntity?> FindByKeyAsync(TKey id, CancellationToken ct = default)
        => db.Set<TEntity>().FirstOrDefaultAsync(e => e.Id!.Equals(id), ct);

    public virtual Task<bool> ExistsAsync(TKey id, CancellationToken ct = default)
        => db.Set<TEntity>().AnyAsync(e => e.Id!.Equals(id), ct);

    // ── Query helpers ─────────────────────────────────────────────────────────

    public virtual Task<long> CountAsync(
        Expression<Func<TEntity, bool>>? where = null, CancellationToken ct = default)
        => where is null
            ? db.Set<TEntity>().LongCountAsync(ct)
            : db.Set<TEntity>().LongCountAsync(where, ct);

    public virtual Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>>? where = null, CancellationToken ct = default)
        => where is null
            ? db.Set<TEntity>().AnyAsync(ct)
            : db.Set<TEntity>().AnyAsync(where, ct);

    public virtual Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> where, CancellationToken ct = default)
        => db.Set<TEntity>().FirstOrDefaultAsync(where, ct);

    public virtual Task<List<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        CancellationToken ct = default)
        => BuildQuery(where, orderBy).ToListAsync(ct);

    public virtual async Task<PagedResult<TEntity>> GetPagedListAsync(
        int page, int pageSize,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        CancellationToken ct = default)
    {
        IQueryable<TEntity> query = BuildQuery(where, orderBy);
        int total = await query.CountAsync(ct);
        List<TEntity> items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<TEntity>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public virtual IQueryable<TEntity> GetQueryable(Expression<Func<TEntity, bool>>? where = null)
        => BuildQuery(where);

    // ── Write ─────────────────────────────────────────────────────────────────

    public virtual void Add(TEntity entity) => db.Set<TEntity>().Add(entity);

    public virtual Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        db.Set<TEntity>().AddRange(entities);
        return Task.CompletedTask;
    }

    public virtual void Update(TEntity entity) => db.Set<TEntity>().Update(entity);

    public virtual Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        db.Set<TEntity>().UpdateRange(entities);
        return Task.CompletedTask;
    }

    public virtual void Remove(TEntity entity) => db.Set<TEntity>().Remove(entity);

    public virtual Task RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
    {
        db.Set<TEntity>().RemoveRange(entities);
        return Task.CompletedTask;
    }

    // ── Protected helpers ─────────────────────────────────────────────────────

    /// <summary>Builds a base <see cref="IQueryable{T}"/> with optional filter and ordering.</summary>
    protected IQueryable<TEntity> BuildQuery(
        Expression<Func<TEntity, bool>>? where,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null)
    {
        IQueryable<TEntity> query = db.Set<TEntity>();
        if (where is not null) query = query.Where(where);
        if (orderBy is not null) query = orderBy(query);
        return query;
    }
}
