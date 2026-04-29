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
/// </summary>
public interface IBaseRepository<TEntity, TKey>
    where TEntity : Entity<TKey>
{
    // ── Read (needed by command handlers for load-then-mutate) ────────────────
    Task<TEntity> GetByKeyAsync(TKey id, CancellationToken ct = default);
    Task<TEntity?> FindByKeyAsync(TKey id, CancellationToken ct = default);
    Task<bool> ExistsAsync(TKey id, CancellationToken ct = default);

    // ── Write ─────────────────────────────────────────────────────────────────
    void Add(TEntity entity);
    void Update(TEntity entity);
    void Remove(TEntity entity);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
