namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Tier 3 system configuration: authentication and security settings.
///     Bound from <c>appsettings.json</c> section <c>Security</c>.
/// </summary>
public record SecurityOptions
{
    public const string Section = "Security";

    /// <summary>JWT access token validity window in minutes. Default: 15.</summary>
    public int AccessTokenExpiryMinutes { get; init; } = 15;

    /// <summary>Refresh token validity in days. Default: 7.</summary>
    public int RefreshTokenExpiryDays { get; init; } = 7;

    /// <summary>Failed login attempts before account lockout. Default: 5.</summary>
    public int MaxFailedLoginAttempts { get; init; } = 5;

    /// <summary>Account lockout duration in minutes. Default: 15.</summary>
    public int LockoutDurationMinutes { get; init; } = 15;

    /// <summary>Maximum API requests per IP per minute. Default: 60.</summary>
    public int RateLimitRequestsPerMinute { get; init; } = 60;
}

