namespace MarketNest.Base.Common;

/// <summary>
///     Provides the current user's time zone and preferred date/time format.
///     Resolved per-request in the Web layer (from claims, profile, or cookie).
///     Falls back to UTC when no user preference is available.
/// </summary>
public interface IUserTimeZoneProvider
{
    /// <summary>The user's preferred time zone. Defaults to UTC.</summary>
    TimeZoneInfo TimeZone { get; }

    /// <summary>The user's preferred date-only format (e.g. "dd/MM/yyyy"). Defaults to ISO.</summary>
    string DateFormat { get; }

    /// <summary>The user's preferred date+time format (e.g. "dd/MM/yyyy HH:mm"). Defaults to ISO.</summary>
    string DateTimeFormat { get; }

    /// <summary>The user's preferred time-only format (e.g. "HH:mm"). Defaults to 24h.</summary>
    string TimeFormat { get; }
}
