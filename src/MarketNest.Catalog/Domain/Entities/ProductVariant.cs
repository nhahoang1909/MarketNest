namespace MarketNest.Catalog.Domain;

/// <summary>
///     ProductVariant aggregate — represents a purchasable SKU variant of a product.
///     Encapsulates base price, optional sale price window, and computed effective price.
/// </summary>
public class ProductVariant : Entity<Guid>
{
    protected ProductVariant() { }

    public Guid ProductId { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public Money Price { get; private set; } = null!;
    public Money? CompareAtPrice { get; private set; }

    // ── Sale Price Fields ──────────────────────────────────────────────
    public Money? SalePrice { get; private set; }
    public DateTimeOffset? SaleStart { get; private set; }
    public DateTimeOffset? SaleEnd { get; private set; }
    // ──────────────────────────────────────────────────────────────────

    public int StockQuantity { get; private set; }
    public VariantStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // ── Factory ────────────────────────────────────────────────────────
    public static ProductVariant Create(
        Guid productId,
        string sku,
        Money price,
        int stockQuantity,
        Money? compareAtPrice = null)
    {
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Sku = sku,
            Price = price,
            CompareAtPrice = compareAtPrice,
            StockQuantity = stockQuantity,
            Status = VariantStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        return variant;
    }

    // ── Computed Helpers ───────────────────────────────────────────────

    /// <summary>
    ///     Returns true when a timed sale is currently active.
    /// </summary>
    public bool IsSaleActive(DateTimeOffset? at = null)
    {
        DateTimeOffset now = at ?? DateTimeOffset.UtcNow;
        return SalePrice is not null
               && SaleStart <= now
               && SaleEnd > now;
    }

    /// <summary>
    ///     The actual price a buyer pays right now (or at a given instant).
    ///     Use this everywhere instead of reading <see cref="Price"/> directly.
    /// </summary>
    public Money EffectivePrice(DateTimeOffset? at = null)
        => IsSaleActive(at) ? SalePrice! : Price;

    /// <summary>
    ///     The strikethrough price shown in the UI.
    ///     Returns <see cref="Price"/> when sale is active, <see cref="CompareAtPrice"/> otherwise, or null.
    /// </summary>
    public Money? DisplayOriginalPrice(DateTimeOffset? at = null)
    {
        if (IsSaleActive(at)) return Price;
        return CompareAtPrice;
    }

    // ── Domain Mutations ──────────────────────────────────────────────

    /// <summary>
    ///     Sets a timed sale price on this variant.
    ///     Overwrites any existing sale (Phase 1: one active sale at a time).
    /// </summary>
    public Result<Unit, Error> SetSalePrice(Money salePrice, DateTimeOffset start, DateTimeOffset end)
    {
        if (salePrice.Amount >= Price.Amount)
            return Result<Unit, Error>.Failure(
                new Error("CATALOG.VARIANT_SALE_PRICE_NOT_LESS_THAN_BASE",
                    "Sale price must be strictly less than base price."));

        if (start >= end)
            return Result<Unit, Error>.Failure(
                new Error("CATALOG.VARIANT_SALE_DATES_INVALID",
                    "Sale start must be before sale end."));

        if (end <= DateTimeOffset.UtcNow)
            return Result<Unit, Error>.Failure(
                new Error("CATALOG.VARIANT_SALE_END_IN_PAST",
                    "Sale end must be in the future."));

        if ((end - start).TotalDays > CatalogConstants.Sale.MaxDurationDays)
            return Result<Unit, Error>.Failure(
                new Error("CATALOG.VARIANT_SALE_DURATION_EXCEEDED",
                    $"Sale period cannot exceed {CatalogConstants.Sale.MaxDurationDays} days."));

        SalePrice = salePrice;
        SaleStart = start;
        SaleEnd = end;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new VariantSalePriceSetEvent(Id, salePrice, start, end));
        return Result<Unit, Error>.Success(Unit.Value);
    }

    /// <summary>
    ///     Removes an active sale price immediately. Idempotent.
    /// </summary>
    public Result<Unit, Error> RemoveSalePrice()
    {
        if (SalePrice is null)
            return Result<Unit, Error>.Success(Unit.Value); // idempotent

        SalePrice = null;
        SaleStart = null;
        SaleEnd = null;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new VariantSalePriceRemovedEvent(Id));
        return Result<Unit, Error>.Success(Unit.Value);
    }
}

