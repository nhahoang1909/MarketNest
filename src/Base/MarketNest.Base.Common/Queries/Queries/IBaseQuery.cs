namespace MarketNest.Base.Common;

/// <summary>
///     Rich read-only contract for query-side infrastructure.
///     <br/>
///     Naming convention:
///     <list type="bullet">
///         <item><term><c>GetByKeyAsync</c></term><description>Throws <see cref="KeyNotFoundException"/> when not found.</description></item>
///         <item><term><c>FindByKeyAsync</c></term><description>Returns <c>null</c> when not found.</description></item>
///     </list>
/// </summary>
public interface IBaseQuery<TEntity, TKey> where TEntity : class
{
    // ── Count / Exists ───────────────────────────────────────────────────────
    Task<long> CountAsync(
        Expression<Func<TEntity, bool>>? where = null, CancellationToken ct = default);

    Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>>? where = null, CancellationToken ct = default);

    // ── Get by key — throws KeyNotFoundException ─────────────────────────────
    Task<TEntity> GetByKeyAsync(TKey id, CancellationToken ct = default);

    Task<TDto> GetByKeyAsync<TDto>(
        TKey id,
        Expression<Func<TEntity, TDto>> selector,
        CancellationToken ct = default);

    // ── Find by key — returns null ────────────────────────────────────────────
    Task<TEntity?> FindByKeyAsync(TKey id, CancellationToken ct = default);

    Task<TDto?> FindByKeyAsync<TDto>(
        TKey id,
        Expression<Func<TEntity, TDto>> selector,
        CancellationToken ct = default);

    // ── FirstOrDefault ────────────────────────────────────────────────────────
    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> where, CancellationToken ct = default);

    Task<TDto?> FirstOrDefaultAsync<TDto>(
        Expression<Func<TEntity, TDto>> selector,
        Expression<Func<TEntity, bool>> where,
        CancellationToken ct = default);

    // ── List ─────────────────────────────────────────────────────────────────
    Task<List<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        CancellationToken ct = default);

    Task<List<TDto>> ListAsync<TDto>(
        Expression<Func<TEntity, TDto>> selector,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        CancellationToken ct = default);

    // ── Paged List ────────────────────────────────────────────────────────────
    Task<PagedResult<TEntity>> GetPagedListAsync(
        int page, int pageSize,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        CancellationToken ct = default);

    Task<PagedResult<TDto>> GetPagedListAsync<TDto>(
        Expression<Func<TEntity, TDto>> selector,
        int page, int pageSize,
        Expression<Func<TEntity, bool>>? where = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        CancellationToken ct = default);

    // ── Queryable access (for complex custom queries) ─────────────────────────
    IQueryable<TEntity> GetQueryable(Expression<Func<TEntity, bool>>? where = null);
}
