namespace MarketNest.Base.Common;

/// <summary>
///     Implemented by Catalog module; consumed by Payments module for commission rates.
/// </summary>
public interface IStorefrontReadService
{
    Task<decimal> GetCommissionRateAsync(Guid storeId, CancellationToken ct = default);
    Task<StorefrontInfo?> GetBySlugAsync(string slug, CancellationToken ct = default);
}

public record StorefrontInfo(Guid Id, string Name, string Slug, Guid SellerId);
