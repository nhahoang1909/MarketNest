namespace MarketNest.Catalog.Application;

/// <summary>
///     Removes the active sale price from a product variant immediately.
///     Sellers can only remove their own variant's sale; admins can remove any.
/// </summary>
public record RemoveSalePriceCommand(
    Guid VariantId,
    Guid RequestingUserId,
    bool IsAdmin = false) : ICommand<Unit>;

