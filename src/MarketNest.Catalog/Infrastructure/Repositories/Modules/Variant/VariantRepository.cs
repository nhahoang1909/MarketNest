using MarketNest.Base.Infrastructure;
using MarketNest.Catalog.Application;
using MarketNest.Catalog.Domain;

namespace MarketNest.Catalog.Infrastructure;

public class VariantRepository(CatalogDbContext db)
    : BaseRepository<ProductVariant, Guid>(db), IVariantRepository
{
    public async Task<IReadOnlyList<ProductVariant>> GetExpiredSalesAsync(
        DateTimeOffset utcNow, CancellationToken ct = default)
        => await Db.Variants
            .Where(v => v.SalePrice != null && v.SaleEnd <= utcNow)
            .ToListAsync(ct);

    public Task<ProductVariant?> GetByProductAsync(
        Guid productId, Guid variantId, CancellationToken ct = default)
        => Db.Variants
            .FirstOrDefaultAsync(v => v.Id == variantId && v.ProductId == productId, ct);
}

