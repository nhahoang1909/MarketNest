namespace MarketNest.Catalog.Domain;

/// <summary>
///     Raised when a seller sets a timed sale price on a variant.
/// </summary>
public record VariantSalePriceSetEvent(
    Guid VariantId,
    Money SalePrice,
    DateTimeOffset SaleStart,
    DateTimeOffset SaleEnd) : IDomainEvent;

