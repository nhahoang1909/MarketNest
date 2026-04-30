namespace MarketNest.Base.Common;

/// <summary>
///     Centralized Redis cache key definitions and TTL constants.
///     All cache keys must be registered here — no magic strings.
///     Key convention: <c>marketnest:{module}:{entity}:{identifier}</c>
/// </summary>
public static class CacheKeys
{
    private const string Prefix = "marketnest";

    // ── Tier 1 — Reference Data (24h TTL) ────────────────────────────────
    public static class ReferenceData
    {
        private const string Base = $"{Prefix}:refdata";

        public const string Countries = $"{Base}:countries";
        public const string Genders = $"{Base}:genders";
        public const string PhoneCodes = $"{Base}:phone-codes";
        public const string Nationalities = $"{Base}:nationalities";
        public const string Categories = $"{Base}:categories";

        /// <summary>Single-country lookup: <c>marketnest:refdata:country:{code}</c></summary>
        public static string Country(string code) => $"{Base}:country:{code.ToUpperInvariant()}";

        /// <summary>Single-category lookup by id: <c>marketnest:refdata:category:{id}</c></summary>
        public static string Category(int id) => $"{Base}:category:{id}";

        /// <summary>Category by slug: <c>marketnest:refdata:category:slug:{slug}</c></summary>
        public static string CategoryBySlug(string slug) => $"{Base}:category:slug:{slug}";

        /// <summary>
        ///     Prefix for bulk invalidation when Admin performs CRUD on reference data (Phase 3).
        /// </summary>
        public const string InvalidationPrefix = $"{Base}:";
    }

    // ── Tier 2 — Business Configuration (1h TTL) ─────────────────────────
    public static class BusinessConfig
    {
        private const string Base = $"{Prefix}:config";

        public const string OrderPolicy = $"{Base}:order-policy";
        public const string CommissionDefault = $"{Base}:commission-default";
        public const string StorefrontPolicy = $"{Base}:storefront-policy";
        public const string ReviewPolicy = $"{Base}:review-policy";

        /// <summary>Per-seller commission override: <c>marketnest:config:commission:seller:{id}</c></summary>
        public static string CommissionForSeller(Guid sellerId) => $"{Base}:commission:seller:{sellerId}";
    }

    // ── Catalog ───────────────────────────────────────────────────────────
    public static class Catalog
    {
        private const string Base = $"{Prefix}:catalog";

        /// <summary>Product detail: <c>marketnest:catalog:product:{id}</c></summary>
        public static string Product(Guid id) => $"{Base}:product:{id}";

        /// <summary>Product variant: <c>marketnest:catalog:variant:{id}</c></summary>
        public static string ProductVariant(Guid id) => $"{Base}:variant:{id}";

        /// <summary>Storefront by slug: <c>marketnest:catalog:storefront:{slug}</c></summary>
        public static string Storefront(string slug) => $"{Base}:storefront:{slug}";

        /// <summary>Storefront by ID: <c>marketnest:catalog:storefront:id:{id}</c></summary>
        public static string StorefrontById(Guid id) => $"{Base}:storefront:id:{id}";

        /// <summary>Average rating for a product: <c>marketnest:catalog:rating:{productId}</c></summary>
        public static string ProductRating(Guid productId) => $"{Base}:rating:{productId}";

        /// <summary>Prefix for bulk invalidation of all catalog cache.</summary>
        public const string InvalidationPrefix = $"{Base}:";
    }

    // ── Cart ──────────────────────────────────────────────────────────────
    public static class Cart
    {
        private const string Base = $"{Prefix}:cart";

        /// <summary>Cart item count for badge display: <c>marketnest:cart:count:{userId}</c></summary>
        public static string Count(Guid userId) => $"{Base}:count:{userId}";
    }

    // ── Payments ──────────────────────────────────────────────────────────
    public static class Payments
    {
        private const string Base = $"{Prefix}:payments";

        /// <summary>Commission rate for a store: <c>marketnest:payments:commission:{storeId}</c></summary>
        public static string CommissionRate(Guid storeId) => $"{Base}:commission:{storeId}";
    }

    // ── Identity ──────────────────────────────────────────────────────────
    public static class Identity
    {
        private const string Base = $"{Prefix}:identity";

        /// <summary>User preferences snapshot: <c>marketnest:identity:prefs:{userId}</c></summary>
        public static string UserPreferences(Guid userId) => $"{Base}:prefs:{userId}";
    }

    // ── Admin / Platform Config ──────────────────────────────────────────
    public static class Admin
    {
        private const string Base = $"{Prefix}:admin";

        /// <summary>Platform-level config: <c>marketnest:admin:config:{key}</c></summary>
        public static string PlatformConfig(string key) => $"{Base}:config:{key}";

        /// <summary>Global platform config: <c>marketnest:admin:config:global</c></summary>
        public const string PlatformConfigGlobal = $"{Base}:config:global";

        /// <summary>Prohibited categories list.</summary>
        public const string ProhibitedCategories = $"{Base}:prohibited-categories";
    }

    // ── TTL presets ────────────────────────────────────────────────────────
    public static class Ttl
    {
        /// <summary>30 seconds — cart count badge, near-realtime data.</summary>
        public static readonly TimeSpan VeryShort = TimeSpan.FromSeconds(30);

        /// <summary>1 minute — product detail with prices.</summary>
        public static readonly TimeSpan QuickExpiry = TimeSpan.FromMinutes(1);

        /// <summary>5 minutes — frequently-updated read models, storefront.</summary>
        public static readonly TimeSpan Brief = TimeSpan.FromMinutes(5);

        /// <summary>30 minutes — medium-frequency read models, commission rates.</summary>
        public static readonly TimeSpan Medium = TimeSpan.FromMinutes(30);

        /// <summary>1 hour — used for Tier 2 business config.</summary>
        public static readonly TimeSpan BusinessConfig = TimeSpan.FromHours(1);

        /// <summary>6 hours — category lists, rarely-changed reference data.</summary>
        public static readonly TimeSpan VeryLong = TimeSpan.FromHours(6);

        /// <summary>24 hours — used for Tier 1 reference data (countries, nationalities).</summary>
        public static readonly TimeSpan ReferenceData = TimeSpan.FromHours(24);
    }
}
