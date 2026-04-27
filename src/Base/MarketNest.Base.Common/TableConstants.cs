namespace MarketNest.Base.Common;

/// <summary>
///     Database table and schema name constants shared across all modules.
///     Eliminates magic strings for schema names, system table names, and column names.
/// </summary>
public static class TableConstants
{
    // ── Schema Names ────────────────────────────────────────────────────

    public static class Schema
    {
        public const string Default = "public";
        public const string Identity = "identity";
        public const string Catalog = "catalog";
        public const string Cart = "cart";
        public const string Orders = "orders";
        public const string Payments = "payments";
        public const string Reviews = "reviews";
        public const string Disputes = "disputes";
        public const string Notifications = "notifications";
        public const string Admin = "admin";
        public const string Auditing = "auditing";
        public const string Promotions = "promotions";
    }

    // ── System Tables (public schema) ───────────────────────────────────

    /// <summary>
    ///     System-level tracking tables managed by <c>DatabaseInitializer</c> / <c>DatabaseTracker</c>.
    ///     These live in the <c>public</c> schema.
    /// </summary>
    public static class SystemTable
    {
        public const string AutoMigrationHistory = "__auto_migration_history";
        public const string SeedHistory = "__seed_history";
    }
}
