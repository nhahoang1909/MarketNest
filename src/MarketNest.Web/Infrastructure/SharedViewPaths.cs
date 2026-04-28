namespace MarketNest.Web.Infrastructure;

/// <summary>
/// Centralized shared view/partial paths used across the Web host.
/// Keep this file minimal; add new shared partial paths here so views can reference a single constant.
/// Namespace follows project convention: stop at the layer level (MarketNest.Web.Infrastructure).
/// </summary>
public static class SharedViewPaths
{
    // Partial that renders the global loading overlay/spinner
    public const string LoadingSpinner = "~/Pages/Shared/Display/_LoadingSpinner.cshtml";
}

