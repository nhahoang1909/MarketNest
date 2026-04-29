namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Centralized application-wide constants. No magic strings or magic numbers.
/// </summary>
public static class AppConstants
{
    // ── Application Metadata ─────────────────────────────────────────
    public const string AppName = "MarketNest";
    public const string AppVersion = "v1.0.0";

    // ── Serilog ──────────────────────────────────────────────────────
    public const string SerilogApplicationProperty = "Application";

    // ── Database ─────────────────────────────────────────────────────
    public const string DefaultConnectionStringName = "DefaultConnection";

    // ── Configuration Keys ───────────────────────────────────────────
    public const string SeqServerUrlKey = "Seq:ServerUrl";

    // ── Copyright ─────────────────────────────────────────────────────
    public const int CopyrightYear = 2026;

    // ── OpenAPI ──────────────────────────────────────────────────────
    public static class OpenApi
    {
        public const string DocumentName = "v1";
        public const string Title = "MarketNest API";
        public const string Version = "1.0.0";
        public const string Description = "Multi-vendor marketplace REST API — browse, buy, sell, and manage orders.";
    }

    // ── Localization ─────────────────────────────────────────────────
    public static class Cultures
    {
        public const string English = "en";
        public const string Vietnamese = "vi";
        public const string Default = English;
        public const string CookieName = ".MarketNest.Culture";

        public static readonly string[] Supported = [English, Vietnamese];
    }

    // ── Cookie Settings ──────────────────────────────────────────────
    public static class Cookies
    {
        public const int CultureExpirationYears = 1;
    }

    // ── User Roles ───────────────────────────────────────────────────
    public static class Roles
    {
        public const string Guest = "guest";
        public const string Buyer = "buyer";
        public const string Seller = "seller";
        public const string Admin = "admin";
        public const string SuperAdmin = "Super Admin";
    }

    // ── UI Defaults ──────────────────────────────────────────────────
    public static class Defaults
    {
        public const string FallbackPageTitle = "Dashboard";
        public const string FallbackUserName = "Admin";
        public const string FallbackSellerName = "Seller";
    }

    // ── Font Configuration ────────────────────────────────────────
    /// <summary>
    ///     Centralized font stacks and CDN URLs. Change fonts here to update the entire system.
    ///     CSS custom properties in input.css (--font-sans, --font-display, --font-mono, --font-admin)
    ///     must stay in sync with these values.
    /// </summary>
    public static class Fonts
    {
        // Font family stacks — used in inline style="" attributes and <style> overrides
        public const string Sans = "'DM Sans', 'Inter', system-ui, sans-serif";
        public const string Display = "'Playfair Display', Georgia, 'Times New Roman', serif";
        public const string Admin = "'DM Sans', 'Inter', system-ui, sans-serif";
        public const string Mono = "'JetBrains Mono', ui-monospace, 'SF Mono', Menlo, monospace";

        // Short stacks — for tight inline style="" where brevity matters (logo, icon text)
        public const string DisplayShort = "'Playfair Display', serif";
        public const string SansShort = "'DM Sans', sans-serif";
        public const string MonoShort = "'JetBrains Mono', monospace";

        // CDN stylesheet URLs — font loading
        public const string DisplayCdn =
            "https://fonts.googleapis.com/css2?family=Playfair+Display:ital,wght@0,400..900;1,400..900&display=swap";

        public const string SansCdn =
            "https://fonts.googleapis.com/css2?family=DM+Sans:ital,opsz,wght@0,9..40,100..1000;1,9..40,100..1000&display=swap";

        public const string MonoCdn =
            "https://fonts.googleapis.com/css2?family=JetBrains+Mono:wght@400;500;600;700&display=swap";

        // Preconnect hints
        public const string GoogleFontsOrigin = "https://fonts.googleapis.com";
        public const string GoogleFontsStaticOrigin = "https://fonts.gstatic.com";
    }

    // ── External CDN Libraries ────────────────────────────────────────
    public static class Cdn
    {
        public const string D3Js = "https://unpkg.com/d3@7.9.0/dist/d3.min.js";
        public const string TopojsonClient = "https://unpkg.com/topojson-client@3.1.0/dist/topojson-client.min.js";
        public const string WorldAtlas = "https://unpkg.com/world-atlas@2.0.2/countries-110m.json";
    }

    // ── Not Found Page ────────────────────────────────────────────────
    public static class NotFoundPage
    {
        public const string PageTitle = "404 — Off the map";
        public const string Eyebrow = "Error 404 · Not found";
        public const string StatusCode = "404";
        public const string Heading = "This page is off the map.";

        public const string Description =
            "We searched every continent and couldn't find what you were looking for. "
            + "The link may have moved, or the coordinates were never quite right. "
            + "Let's get you back to somewhere familiar.";

        public const string BackToHome = "Back to home";
        public const string BrowseShop = "Report a broken link";
        public const string FooterStatus = "Status: lost in transit";
        public const string CoordinateFormat = "LAT  00.0° · LON 000.0°";
        public const string SignalLost = "Signal lost";
        public const string TraceId = "Trace #4F-0G3";
        public const string DragHint = "Drag to spin";
        public const string BreadcrumbError = "404 · Page not found";

        // Suggested destination cards
        public const string SugBrowseLabel = "Browse";
        public const string SugBrowseTitle = "Markets & collections";
        public const string SugAccountLabel = "Account";
        public const string SugAccountTitle = "Your orders";
        public const string SugHelpLabel = "Help";
        public const string SugHelpTitle = "Seller support";

        // Routes — Note: actual route definitions live in AppRoutes
        // These are just reference constants for the NotFound page template
        // The href attributes in the Razor markup still use AppRoutes directly
    }

    // ── Error Page ───────────────────────────────────────────────────
    public static class ErrorPage
    {
        public const string PageTitle = "500 — Something broke on our end";
        public const string Eyebrow = "Error 500 · Internal server error";
        public const string StatusCode = "500";
        public const string Heading = "Something broke on our end.";

        public const string Description =
            "One of our services stopped responding mid-request. Our team has been notified and is already looking into it. Please try again in a moment — most hiccups clear within a minute or two.";

        public const string TryAgain = "Try again";
        public const string FooterStatus = "Status: degraded · core service";
        public const string SignalLabel = "SIGNAL";
        public const string SignalStatus = "LOST";
        public const string RetryLabel = "RETRY IN";
        public const int RetryCountdownSeconds = 15;
    }

    // ── Design Tokens (Inline Colors) ────────────────────────────
    /// <summary>
    ///     Hex color constants used in server-rendered inline styles.
    ///     These match the Tailwind design tokens in input.css — Starbucks-inspired palette.
    /// </summary>
    public static class Colors
    {
        // Ink palette — House Green based
        public const string Ink = "#1E3932";
        public const string InkLight = "#2b5148";

        // Accent — Starbucks Green
        public const string Accent = "#00754A";
        public const string AccentDark = "#006241";
        public const string AccentDarker = "#004f34";
        public const string AccentDeep = "#1E3932";
        public const string AccentMuted = "#2b5148";
        public const string AccentBg = "#d4e9e2";
        public const string AccentBorder = "#a8d4c4";
        public const string AccentNavActive = "#00754A";

        // Forest — House Green deep
        public const string Forest = "#1E3932";
        public const string ForestLight = "#162d28";

        // Neutral
        public const string NavInactive = "#6b6a66";
        public const string BodyBgAdmin = "#f2f0eb";
        public const string BodyBgSeller = "#f2f0eb";
        public const string LogoBgSeller = "#1E3932";
        public const string TopbarBg = "rgba(255,255,255,0.92)";
        public const string TopbarBgSeller = "rgba(255,255,255,0.92)";

        // Stat card gradients — green tiers
        public const string StatForestFrom = "#1E3932";
        public const string StatForestTo = "#162d28";
        public const string StatAccentFrom = "#00754A";
        public const string StatAccentTo = "#006241";
        public const string StatMustardFrom = "#cba258";
        public const string StatMustardTo = "#b58a3a";

        // Status badge colors — green tinted
        public const string BadgeActiveBg = "#d4e9e2";
        public const string BadgeActiveText = "#004f34";
        public const string BadgeSuspendedBg = "#fdf2f0";
        public const string BadgeSuspendedText = "#c82014";
        public const string BadgeBuyerBg = "#dceaf4";
        public const string BadgeBuyerText = "#1d4360";
        public const string BadgeSellerBg = "#d4e9e2";
        public const string BadgeSellerText = "#004f34";
        public const string BadgeAdminBg = "#d4e9e2";
        public const string BadgeAdminText = "#1E3932";

        // Warning palette
        public const string WarningBg = "#faf6ee";
        public const string WarningText = "#96702e";
        public const string WarningTextDark = "#755724";
        public const string WarningBorder = "#cba258";

        // Success
        public const string SuccessBg = "#f0faf5";
        public const string SuccessText = "#00754A";
        public const string SuccessTextDark = "#006241";
        public const string SuccessTextLight = "#3f8a62";

        // Seller avatar gradient — green accent
        public const string SellerAvatarFrom = "#00754A";
        public const string SellerAvatarTo = "#006241";

     // ── Pending/Order status — gold accent
         public const string PendingBg = "#faf6ee";
         public const string PendingText = "#96702e";
     }

     // ── Cache Duration (seconds) ──────────────────────────────────────
     public static class CommonHeaders
     {
         // Cache control headers (RFC 7234)
         public const string CacheForever = "public, max-age=31536000, immutable";       // 1 year: fingerprinted assets
         public const string Cache1Day = "public, max-age=86400";                       // 1 day: media/fonts
         public const string NoCache = "no-cache";                                      // Revalidate on each request
     }

     // ── Static File Extensions ────────────────────────────────────────
     /// <summary>
     ///     File extension constants used for cache policy decisions and type detection.
     ///     All values are lowercase with leading dot (e.g., ".png").
     /// </summary>
     public static class FileExtensions
     {
         // Image formats
         public const string Png = ".png";
         public const string Jpg = ".jpg";
         public const string Jpeg = ".jpeg";
         public const string WebP = ".webp";
         public const string Svg = ".svg";
         public const string Ico = ".ico";

         // Font formats
         public const string Woff = ".woff";
         public const string Woff2 = ".woff2";

         // Media and font extensions that should be cached for 1 day
         public static readonly string[] CachableMediaExtensions =
         [
             Png, Jpg, Jpeg, WebP, Svg, Ico, Woff2, Woff
         ];
     }

     // ── Output Cache Query Parameters ──────────────────────────────────
     /// <summary>
     ///     Query parameter and route value names used in OutputCache vary-by rules.
     ///     Centralized to prevent typos and support cache key invalidation.
     /// </summary>
     public static class CacheVaryParams
     {
         // Output cache query parameters (SetVaryByQuery)
         public const string Query = "q";
         public const string Category = "category";
         public const string Sort = "sort";
         public const string Page = "page";

         // Output cache route values (SetVaryByRouteValue)
         public const string Slug = "slug";
         public const string ProductId = "productId";

         // Static file fingerprinting query parameter
         public const string VersionQuery = "v";

         // Form parameters
         public const string Culture = "culture";
         public const string ReturnUrl = "returnUrl";
     }

     // ── OutputCache Tag Constants ──────────────────────────────────────
     /// <summary>
     ///     Cache tag names for OutputCache policy eviction.
     ///     Tags are used to group cache entries for bulk invalidation.
     /// </summary>
     public static class CacheTags
     {
         public const string PublicPages = "public";
         public const string StorefrontPages = "storefront";
         public const string ProductPages = "product";
     }

     // ── Cache Duration (as TimeSpan) ───────────────────────────────────
     /// <summary>
     ///     TimeSpan constants for OutputCache policy expiration.
     ///     Do not use directly in Program.cs — use numeric timeouts instead.
     ///     Provided for reference and testing utilities.
     /// </summary>
     public static class CacheDurations
     {
         public static readonly TimeSpan AnonymousPublic = TimeSpan.FromSeconds(60);    // Anonymous home, search
         public static readonly TimeSpan Storefront = TimeSpan.FromMinutes(5);          // Seller storefront pages
         public static readonly TimeSpan ProductDetail = TimeSpan.FromMinutes(2);       // Product prices may change
         public static readonly TimeSpan OneYear = TimeSpan.FromSeconds(31536000);      // Fingerprinted assets
         public static readonly TimeSpan OneDay = TimeSpan.FromSeconds(86400);          // Media/fonts
     }

     // ── Validation Constraints ────────────────────────────────────────
     /// <summary>
     ///     Platform-wide validation rules: password length, username length, file upload limits.
     ///     These are business constants that never change per environment.
     ///     Environment-specific settings (rate limiting, lockout duration) remain in appsettings.json.
     /// </summary>
     public static class Validation
     {
         // Password constraints
         public const int PasswordMinLength = 8;
         public const int PasswordMaxLength = 128;

         // Username constraints
         public const int UsernameMinLength = 3;
         public const int UsernameMaxLength = 50;

         // File upload limits
         public const int MaxImageSizeBytes = 5_242_880;              // 5 MB
         public const int MaxProductImagesPerUpload = 5;
         public const int MaxDocumentSizeBytes = 10_485_760;          // 10 MB
     }
 }
