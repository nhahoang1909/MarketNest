using MarketNest.Base.Infrastructure;
using MarketNest.Catalog.Domain;

namespace MarketNest.Catalog.Application;

/// <summary>
///     Write-side repository for <see cref="ProductVariant"/> aggregate.
///     Only Application-layer command handlers may inject this interface.
/// </summary>
public interface IVariantRepository : IBaseRepository<ProductVariant, Guid>
{
    /// <summary>
    ///     Returns all variants whose sale window has already ended but sale fields
    ///     have not yet been cleared. Used by <c>ExpireSalesJob</c>.
    /// </summary>
    Task<IReadOnlyList<ProductVariant>> GetExpiredSalesAsync(
        DateTimeOffset utcNow, CancellationToken ct = default);

    /// <summary>
    ///     Returns a variant belonging to the given product.
    ///     Returns null when the variant does not exist or does not belong to the product.
    /// </summary>
    Task<ProductVariant?> GetByProductAsync(
        Guid productId, Guid variantId, CancellationToken ct = default);
}

