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

    public static class AuditingTable
    {
        public const string AuditLog = "audit_logs";
        public const string LoginEvent = "login_events";
    }

    public static class PromotionTable
    {
        public const string Voucher = "vouchers";
        public const string VoucherUsage = "voucher_usages";
    }

    public static class AdminTable
    {
        public const string Announcement = "announcements";
    }

    public static class CartsTable { }

    public static class CatalogTable
    {
        public const string Variant = "variants";
    }

    public static class DisputesTable { }

    public static class IdentityTable { }

    public static class NotificationTable
    {
        public const string Notification = "notifications";
        public const string NotificationTemplate = "notification_templates";
    }

    public static class OrdersTable
    {
        public const string OrderPolicyConfig = "order_policy_config";
    }

    public static class PaymentsTable
    {
        public const string CommissionPolicy = "commission_policies";
    }

    public static class ReviewsTable { }

    /// <summary>
    ///     Shared reference data tables that live in the <c>public</c> schema
    ///     and are managed by the Admin module but readable across all modules.
    /// </summary>
    public static class ReferenceTable
    {
        public const string Country = "countries";
        public const string Gender = "genders";
        public const string Nationality = "nationalities";
        public const string PhoneCountryCode = "phone_country_codes";
        public const string ProductCategory = "product_categories";
    }
}
