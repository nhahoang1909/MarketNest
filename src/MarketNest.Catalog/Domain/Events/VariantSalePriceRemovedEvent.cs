namespace MarketNest.Catalog.Domain;

/// <summary>
///     Raised when a sale price is removed from a variant (manually or by the expiry job).
/// </summary>
public record VariantSalePriceRemovedEvent(Guid VariantId) : IDomainEvent;

