namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Centralized route definitions. All UI hrefs and redirects must reference these constants.
/// </summary>
public static class AppRoutes
{
    // ── Pages ─────────────────────────────────────────────────────────
    public const string Home = "/";
    public const string Shop = "/shop";
    public const string Cart = "/cart";
    public const string Search = "/search";
    public const string SearchSuggestions = "/search/suggestions";
    public const string Error = "/Error";
    public const string NotFound = "/not-found";
    public const string Health = "/health";

    // ── Whitelist: all allowed path prefixes ─────────────────────────
    /// <summary>
    ///     Only requests whose path starts with one of these prefixes are allowed.
    ///     Static files (/css, /js, /lib, etc.) are served before this middleware runs.
    /// </summary>
    public static readonly HashSet<string> WhitelistedPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        Home,
        Shop,
        Cart,
        Search,
        Error,
        NotFound,
        Health,
        Auth.Login,
        Auth.Register,
        Auth.ForgotPassword,
        Account.Settings,
        Account.Orders,
        Account.Wishlist,
        Account.Disputes,
        Seller.Dashboard,
        Seller.Storefront,
        Seller.Products,
        Seller.ProductImport,
        Seller.ProductExport,
        Seller.ProductImportTemplate,
        Seller.Orders,
        Seller.Payouts,
        Seller.Reviews,
        Seller.Disputes,
        Admin.Dashboard,
        Admin.Users,
        Admin.Storefronts,
        Admin.Disputes,
        Admin.Notifications,
        Admin.ConfigPrefix,
        Checkout.Index,
        Api.SetLanguage,
        Api.OpenApiDoc,
        Api.ScalarDocs,
        Api.AdminV1Prefix,
        Api.PromotionsV1Prefix,
        Api.CatalogV1Prefix,
        Api.UploadsV1Prefix
    };

    /// <summary>
    ///     Checks if the given path is allowed by the whitelist.
    /// </summary>
    public static bool IsAllowed(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // Exact match for "/"
        if (path == Home)
            return true;

        return WhitelistedPrefixes.Any(prefix =>
            prefix != Home && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    // ── Auth ──────────────────────────────────────────────────────────
    public static class Auth
    {
        public const string Login = "/auth/login";
        public const string Register = "/auth/register";
        public const string ForgotPassword = "/auth/forgot-password";

        public static string RegisterSeller => $"{Register}?role=seller";
    }

    // ── API ──────────────────────────────────────────────────────────
    public static class Api
    {
        public const string SetLanguage = "/api/set-language";
        public const string OpenApiDoc = "/openapi";
        public const string ScalarDocs = "/scalar";
        public const string AdminV1Prefix = "/api/v1/admin";
        public const string PromotionsV1Prefix = "/api/v1/promotions";
        public const string CatalogV1Prefix = "/api/v1/seller/products";
        public const string UploadsV1Prefix = "/api/v1/uploads";
    }

    // ── Account ──────────────────────────────────────────────────────
    public static class Account
    {
        public const string Settings = "/account/settings";
        public const string Orders = "/account/orders";
        public const string Wishlist = "/account/wishlist";
        public const string Disputes = "/account/disputes";
    }

    // ── Seller ───────────────────────────────────────────────────────
    public static class Seller
    {
        public const string Prefix = "/seller";
        public const string Dashboard = "/seller/dashboard";
        public const string Storefront = "/seller/storefront";
        public const string Products = "/seller/products";
        public const string ProductImport = "/seller/products/import";
        public const string ProductExport = "/seller/products/export";
        public const string ProductImportTemplate = "/seller/products/import/template";
        public const string Orders = "/seller/orders";
        public const string Payouts = "/seller/payouts";
        public const string Reviews = "/seller/reviews";
        public const string Disputes = "/seller/disputes";
    }

    // ── Admin ────────────────────────────────────────────────────────
    public static class Admin
    {
        public const string Prefix = "/admin";
        public const string Dashboard = "/admin/dashboard";
        public const string Users = "/admin/users";
        public const string Storefronts = "/admin/storefronts";
        public const string Disputes = "/admin/disputes";
        public const string Notifications = "/admin/notifications";
        public const string ConfigPrefix = "/admin/config";
        public const string ConfigCommission = "/admin/config/commission";
        public const string ConfigCountry = "/admin/config/country";
        public const string ConfigGender = "/admin/config/gender";
        public const string ConfigPhoneCode = "/admin/config/phone-code";
        public const string ConfigProductCategory = "/admin/config/product-category";
        public const string ConfigNationality = "/admin/config/nationality";
    }

    // ── Checkout ─────────────────────────────────────────────────────
    public static class Checkout
    {
        public const string Index = "/checkout";
    }

    // ── Orders ───────────────────────────────────────────────────────
    public static class OrderPages
    {
        public static string Confirmation(Guid orderId) => $"/orders/{orderId}/confirmation";
    }

    // ── Shop query helpers ───────────────────────────────────────────
    public static class ShopQuery
    {
        public static string Category(string slug) => $"{Shop}?category={slug}";
        public static string Sort(string sort) => $"{Shop}?sort={sort}";
        public static string Sale() => $"{Shop}?sale=1";

        public static string Product(string shopSlug, Guid productId) =>
            $"{Shop}/{shopSlug}/products/{productId}";
    }
}
