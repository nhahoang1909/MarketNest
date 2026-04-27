using MarketNest.Base.Infrastructure;

namespace MarketNest.Admin.Infrastructure;

public abstract class BaseRepository<TEntity, TKey>(AdminDbContext db)
    : IBaseRepository<TEntity, TKey>
    where TEntity : Entity<TKey>
{
    protected AdminDbContext Db => db;

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
