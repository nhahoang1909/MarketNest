using MediatR;

namespace MarketNest.Base.Common;

/// <summary>
///     Implemented by Catalog module; consumed by Orders and Cart modules.
/// </summary>
public interface IInventoryService
{
    Task<bool> HasStockAsync(Guid variantId, int quantity, CancellationToken ct = default);
    Task<Result<Unit, Error>> ReserveAsync(Guid variantId, int quantity, Guid cartId, CancellationToken ct = default);
    Task ReleaseAsync(Guid variantId, int quantity, CancellationToken ct = default);
    Task CommitAsync(Guid variantId, int quantity, CancellationToken ct = default);
}
