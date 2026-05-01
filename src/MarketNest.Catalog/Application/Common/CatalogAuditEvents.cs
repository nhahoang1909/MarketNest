namespace MarketNest.Catalog.Application;

/// <summary>
///     Centralized audit event type constants for the Catalog module.
///     Use these constants in <c>[Audited(CatalogAuditEvents.X)]</c> attributes instead of
///     inline magic strings — ensures all catalog audit event names are discoverable in one place.
/// </summary>
/// <remarks>
///     Convention: &lt;MODULE&gt;.&lt;AGGREGATE&gt;_&lt;ACTION&gt; in UPPER_SNAKE_CASE.
///     Each nested class groups events by aggregate root.
/// </remarks>
public static class CatalogAuditEvents
{
    /// <summary>Audit events raised by <c>ProductVariant</c> aggregate operations.</summary>
    public static class Variant
    {
        public const string BulkImport = "CATALOG.VARIANT_BULK_IMPORT";
        public const string SalePriceSet = "CATALOG.VARIANT_SALE_PRICE_SET";
        public const string SalePriceExpired = "CATALOG.VARIANT_SALE_PRICE_EXPIRED";
    }

    /// <summary>Audit events raised by <c>Product</c> aggregate operations.</summary>
    public static class Product
    {
        public const string Created = "CATALOG.PRODUCT_CREATED";
        public const string Updated = "CATALOG.PRODUCT_UPDATED";
        public const string Published = "CATALOG.PRODUCT_PUBLISHED";
        public const string Unpublished = "CATALOG.PRODUCT_UNPUBLISHED";
        public const string Deleted = "CATALOG.PRODUCT_DELETED";
    }

    /// <summary>Audit events raised by <c>Storefront</c> aggregate operations.</summary>
    public static class Storefront
    {
        public const string Created = "CATALOG.STOREFRONT_CREATED";
        public const string Updated = "CATALOG.STOREFRONT_UPDATED";
        public const string Suspended = "CATALOG.STOREFRONT_SUSPENDED";
        public const string Reinstated = "CATALOG.STOREFRONT_REINSTATED";
    }
}

