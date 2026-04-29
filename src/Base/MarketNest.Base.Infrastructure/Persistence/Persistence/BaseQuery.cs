using MarketNest.Base.Common;
using MarketNest.Base.Domain;

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     Shared abstract base for read-side query classes.
///     Inherit in each module passing the module's read <see cref="DbContext"/> type.
/// </summary>
/// <example>
///     <code>
///     public abstract class BaseQuery&lt;TEntity, TKey&gt;(CatalogReadDbContext db)
///         : BaseQuery&lt;TEntity, TKey, CatalogReadDbContext&gt;(db);
///     </code>
/// </example>
public abstract class BaseQuery<TEntity, TKey, TContext>(TContext db)
    : IBaseQuery<TEntity, TKey>
    where TEntity : Entity<TKey>
    where TContext : DbContext
{
    protected TContext Db => db;

    // ── Count / Exists ───────────────────────────────────────────────────────

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

    // ── Get by key — throws ───────────────────────────────────────────────────

    public virtual async Task<TEntity> GetByKeyAsync(TKey id, CancellationToken ct = default)
        => await FindByKeyAsync(id, ct)
           ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} '{id}' not found.");

    public virtual async Task<TDto> GetByKeyAsync<TDto>(
        TKey id,
        Expression<Func<TEntity, TDto>> selector,
        CancellationToken ct = default)
        => await FindByKeyAsync(id, selector, ct)
           ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} '{id}' not found.");

    // ── Find by key — returns null ────────────────────────────────────────────

    public virtual Task<TEntity?> FindByKeyAsync(TKey id, CancellationToken ct = default)
        => db.Set<TEntity>().FirstOrDefaultAsync(e => e.Id!.Equals(id), ct);

    public virtual Task<TDto?> FindByKeyAsync<TDto>(
        TKey id,
        Expression<Func<TEntity, TDto>> selector,
        CancellationToken ct = default)
        => db.Set<TEntity>()
            .Where(e => e.Id!.Equals(id))
            .Select(selector)
            .FirstOrDefaultAsync(ct);

    // ── FirstOrDefault ────────────────────────────────────────────────────────

    public virtual Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> where, CancellationToken ct = default)
        => db.Set<TEntity>().FirstOrDefaultAsync(where, ct);

    public virtual Task<TDto?> FirstOrDefaultAsync<TDto>(
        Expression<Func<TEntity, TDto>> selector,
        Expression<Func<TEntity, bool>> where,
        CancellationToken ct = default)
        => db.Set<TEntity>()
            .Where(where)
            .Select(selector)
            .FirstOrDefaultAsync(ct);

    // ── List ─────────────────────────────────────────────────────────────────

    public virtual Task<List<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        CancellationToken ct = default)
        => BuildQuery(where, orderBy).ToListAsync(ct);

    public virtual Task<List<TDto>> ListAsync<TDto>(
        Expression<Func<TEntity, TDto>> selector,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        CancellationToken ct = default)
        => BuildQuery(where, orderBy).Select(selector).ToListAsync(ct);

    // ── Paged List ────────────────────────────────────────────────────────────

    public virtual async Task<PagedResult<TEntity>> GetPagedListAsync(
        int page, int pageSize,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        CancellationToken ct = default)
    {
        IQueryable<TEntity> query = BuildQuery(where, orderBy);
        int total = await query.CountAsync(ct);
        List<TEntity> items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<TEntity> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    public virtual async Task<PagedResult<TDto>> GetPagedListAsync<TDto>(
        Expression<Func<TEntity, TDto>> selector,
        int page, int pageSize,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        CancellationToken ct = default)
    {
        IQueryable<TEntity> query = BuildQuery(where, orderBy);
        int total = await query.CountAsync(ct);
        List<TDto> items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(selector).ToListAsync(ct);
        return new PagedResult<TDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = total };
    }

    // ── Queryable access ──────────────────────────────────────────────────────

    public virtual IQueryable<TEntity> GetQueryable(Expression<Func<TEntity, bool>>? where = null)
        => BuildQuery(where);

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

