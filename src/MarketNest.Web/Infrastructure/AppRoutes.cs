namespace MarketNest.Web.Infrastructure;

/// <summary>
/// Centralized route definitions. All UI hrefs and redirects must reference these constants.
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
    public const string Health = "/health";

    // ── Auth ──────────────────────────────────────────────────────────
    public static class Auth
    {
        public const string Login = "/auth/login";
        public const string Register = "/auth/register";

        public static string RegisterSeller => $"{Register}?role=seller";
    }

    // ── API ──────────────────────────────────────────────────────────
    public static class Api
    {
        public const string SetLanguage = "/api/set-language";
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
        public const string Dashboard = "/seller/dashboard";
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

    // ── Whitelist: all allowed path prefixes ─────────────────────────
    /// <summary>
    /// Only requests whose path starts with one of these prefixes are allowed.
    /// Static files (/css, /js, /lib, etc.) are served before this middleware runs.
    /// </summary>
    public static readonly HashSet<string> WhitelistedPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        Home,
        Shop,
        Cart,
        Search,
        Error,
        Health,
        Auth.Login,
        Auth.Register,
        Account.Settings,
        Account.Orders,
        Account.Wishlist,
        Account.Disputes,
        Seller.Dashboard,
        Api.SetLanguage,
    };

    /// <summary>
    /// Checks if the given path is allowed by the whitelist.
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
}
