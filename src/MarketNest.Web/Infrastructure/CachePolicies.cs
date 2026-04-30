namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Named OutputCache policy identifiers. Applied via <c>[OutputCache(PolicyName = ...)]</c> on Razor Pages.
///     Policies are registered in <c>Program.cs</c> via <c>AddOutputCache</c>.
/// </summary>
public static class CachePolicies
{
    /// <summary>Public anonymous pages (home, search) — 60 seconds, varies by query.</summary>
    public const string AnonymousPublic = "AnonymousPublic";

    /// <summary>Storefront pages — 5 minutes, varies by slug.</summary>
    public const string Storefront = "Storefront";

    /// <summary>Product detail pages — 2 minutes, varies by slug + productId.</summary>
    public const string ProductDetail = "ProductDetail";
}

