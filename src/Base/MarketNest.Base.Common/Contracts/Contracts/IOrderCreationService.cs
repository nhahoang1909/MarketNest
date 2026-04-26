using MarketNest.Core.Common;
using MarketNest.Core.ValueObjects;

namespace MarketNest.Base.Common;

/// <summary>
///     Implemented by Orders module; consumed by Cart module.
/// </summary>
public interface IOrderCreationService
{
    Task<Result<Guid, Error>> CreateFromCartAsync(
        Guid buyerId,
        CartSnapshot cart,
        Address shippingAddress,
        CancellationToken ct = default);
}

/// <summary>
///     Snapshot of a cart passed between modules (serializable).
/// </summary>
public record CartSnapshot(Guid BuyerId, IReadOnlyList<CartItemSnapshot> Items, Address ShippingAddress);

public record CartItemSnapshot(Guid VariantId, Guid StoreId, string Title, Money UnitPrice, int Quantity);
