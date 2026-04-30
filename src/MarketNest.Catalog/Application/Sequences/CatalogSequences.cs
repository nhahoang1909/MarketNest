using MarketNest.Base.Common;

namespace MarketNest.Catalog.Application;

/// <summary>
/// Sequence descriptors for the Catalog module.
/// All sequences use the "catalog" PostgreSQL schema.
/// </summary>
public static class CatalogSequences
{
    /// <summary>
    /// Never-reset SKU number. Permanent single sequence.
    /// Output: SKU-00001
    /// Capacity: 99,999 SKUs total (permanent)
    /// </summary>
    public static readonly SequenceDescriptor SkuNumber = new(
        schema: TableConstants.Schema.Catalog,
        baseName: "sku",
        prefix: "SKU",
        padWidth: 5,
        resetPeriod: SequenceResetPeriod.Never);
}

