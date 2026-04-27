using MarketNest.Base.Domain;

namespace MarketNest.Base.Infrastructure;

/// <summary>
///     Base repository contract. Only for aggregates — queries bypass repositories.
/// </summary>
public interface IBaseRepository<TEntity, TKey>
    where TEntity : Entity<TKey>
{
    Task<TEntity?> GetByKeyAsync(TKey id, CancellationToken ct = default);
    Task<TEntity> GetByKeyOrThrowAsync(TKey id, CancellationToken ct = default);
    Task<bool> ExistsAsync(TKey id, CancellationToken ct = default);

    void Add(TEntity entity);
    void Update(TEntity entity);
    void Remove(TEntity entity);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
