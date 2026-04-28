namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Tier 3 system configuration: platform-level constants.
///     Bound from <c>appsettings.json</c> section <c>Platform</c>.
///     Changes require redeployment — not editable via Admin UI.
/// </summary>
public record PlatformOptions
{
    public const string Section = "Platform";

    public string PlatformName { get; init; } = "MarketNest";
    public string SupportEmail { get; init; } = "support@marketnest.com";
    public string DefaultTimezone { get; init; } = "UTC";

    /// <summary>Fixed marketplace currency (VND). Not a user-selectable dropdown.</summary>
    public string DefaultCurrency { get; init; } = "VND";

    public string DefaultLanguage { get; init; } = "en";
}

