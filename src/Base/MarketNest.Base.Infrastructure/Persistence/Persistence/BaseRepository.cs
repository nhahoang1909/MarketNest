using MarketNest.Base.Domain;

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     Shared abstract base for write-side repository classes.
///     Inherit in each module passing the module's write <see cref="DbContext"/> type.
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

    // ── Write ─────────────────────────────────────────────────────────────────

    public virtual void Add(TEntity entity) => db.Set<TEntity>().Add(entity);
    public virtual void Update(TEntity entity) => db.Set<TEntity>().Update(entity);
    public virtual void Remove(TEntity entity) => db.Set<TEntity>().Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}

