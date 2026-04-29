using MarketNest.Base.Infrastructure;
using MarketNest.Catalog.Domain;

namespace MarketNest.Catalog.Infrastructure;

/// <summary>
///     Module-local BaseRepository wired to <see cref="CatalogDbContext"/>.
///     Follow the same pattern used by Admin and Promotions modules.
/// </summary>
public abstract class BaseRepository<TEntity, TKey>(CatalogDbContext db)
    : IBaseRepository<TEntity, TKey>
    where TEntity : Entity<TKey>
{
    protected CatalogDbContext Db => db;

    public virtual Task<TEntity?> GetByKeyAsync(TKey id, CancellationToken ct = default)
        => db.Set<TEntity>().FirstOrDefaultAsync(e => e.Id!.Equals(id), ct);

    public virtual async Task<TEntity> GetByKeyOrThrowAsync(TKey id, CancellationToken ct = default)
        => await GetByKeyAsync(id, ct)
           ?? throw new KeyNotFoundException($"{typeof(TEntity).Name} '{id}' not found");

    public virtual Task<bool> ExistsAsync(TKey id, CancellationToken ct = default)
        => db.Set<TEntity>().AnyAsync(e => e.Id!.Equals(id), ct);

    public virtual void Add(TEntity entity) => db.Set<TEntity>().Add(entity);
    public virtual void Update(TEntity entity) => db.Set<TEntity>().Update(entity);
    public virtual void Remove(TEntity entity) => db.Set<TEntity>().Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}

