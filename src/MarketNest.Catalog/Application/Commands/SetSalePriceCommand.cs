namespace MarketNest.Catalog.Application;

/// <summary>
///     Sets a timed sale price on a product variant.
///     The authenticating seller must own the product.
/// </summary>
public record SetSalePriceCommand(
    Guid ProductId,
    Guid VariantId,
    Guid RequestingUserId,
    decimal SalePrice,
    DateTimeOffset SaleStart,
    DateTimeOffset SaleEnd) : ICommand<Unit>;

