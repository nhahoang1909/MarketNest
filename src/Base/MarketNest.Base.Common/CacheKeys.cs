namespace MarketNest.Base.Common;

/// <summary>
///     Centralized Redis cache key definitions and TTL constants.
///     All cache keys must be registered here — no magic strings.
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

    // ── TTL presets ────────────────────────────────────────────────────────
    public static class Ttl
    {
        /// <summary>24 hours — used for Tier 1 reference data.</summary>
        public static readonly TimeSpan ReferenceData = TimeSpan.FromHours(24);

        /// <summary>1 hour — used for Tier 2 business config.</summary>
        public static readonly TimeSpan BusinessConfig = TimeSpan.FromHours(1);

        /// <summary>5 minutes — used for frequently-updated read models.</summary>
        public static readonly TimeSpan Brief = TimeSpan.FromMinutes(5);

        /// <summary>30 minutes — used for medium-frequency read models.</summary>
        public static readonly TimeSpan Medium = TimeSpan.FromMinutes(30);
    }
}

