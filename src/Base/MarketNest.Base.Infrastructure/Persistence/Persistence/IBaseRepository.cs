using MarketNest.Base.Common;
using MarketNest.Base.Domain;

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     Write-side repository contract for aggregates.
///     Query handlers must use <c>IBaseQuery&lt;TEntity, TKey&gt;</c> instead.
///     <br/>
///     Naming convention:
///     <list type="bullet">
///         <item><term><c>GetByKeyAsync</c></term><description>Throws <see cref="KeyNotFoundException"/> when not found.</description></item>
///         <item><term><c>FindByKeyAsync</c></term><description>Returns <c>null</c> when not found.</description></item>
///     </list>
///     <br/>
///     All changes must be committed via <c>IUnitOfWork.CommitAsync</c> — do NOT call SaveChangesAsync directly.
/// </summary>
public interface IBaseRepository<TEntity, TKey>
    where TEntity : Entity<TKey>
{
    // ── Read (needed by command handlers for load-then-mutate) ────────────────
    Task<TEntity> GetByKeyAsync(TKey id, CancellationToken ct = default);
    Task<TEntity?> FindByKeyAsync(TKey id, CancellationToken ct = default);
    Task<bool> ExistsAsync(TKey id, CancellationToken ct = default);

    // ── Query helpers ─────────────────────────────────────────────────────────
    Task<long> CountAsync(Expression<Func<TEntity, bool>>? where = null, CancellationToken ct = default);
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>>? where = null, CancellationToken ct = default);
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> where, CancellationToken ct = default);
    Task<List<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        CancellationToken ct = default);
    Task<PagedResult<TEntity>> GetPagedListAsync(
        int page, int pageSize,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        CancellationToken ct = default);
    IQueryable<TEntity> GetQueryable(Expression<Func<TEntity, bool>>? where = null);

    // ── Write ─────────────────────────────────────────────────────────────────
    void Add(TEntity entity);
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);

    void Update(TEntity entity);
    Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);

    void Remove(TEntity entity);
    Task RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
}
