namespace MarketNest.Catalog.Domain;

/// <summary>
///     Catalog-module-specific constants. Domain rules that belong to this bounded context only.
///     Cross-module constants (pagination defaults, currency codes, etc.) live in
///     <see cref="MarketNest.Base.Common.DomainConstants"/>.
/// </summary>
public static class CatalogConstants
{
    // ── Sale Price Rules ────────────────────────────────────────────────
    public static class Sale
    {
        /// <summary>Maximum allowed sale window in days (invariant S4).</summary>
        public const int MaxDurationDays = 90;

        /// <summary>Schedule interval string for the ExpireSalesJob.</summary>
        public const string ExpiryJobSchedule = "00:05:00";

        /// <summary>Job key — must be globally unique across all modules.</summary>
        public const string ExpiryJobKey = "catalog.variant.expire-sales";
    }
}

