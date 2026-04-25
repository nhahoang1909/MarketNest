using System.Globalization;
using System.Text;

namespace MarketNest.Core.Common;

/// <summary>
///     Extension methods for <see cref="DateTimeOffset"/> that convert to user-local time
///     and format as strings. All format methods output in the user's local time zone.
/// </summary>
public static class DateTimeOffsetExtensions
{
    // Cached CompositeFormat instances for CA1863 compliance
    private static readonly CompositeFormat SecondAgoFormat = CompositeFormat.Parse(DomainConstants.RelativeTime.SecondAgo);
    private static readonly CompositeFormat MinuteAgoFormat = CompositeFormat.Parse(DomainConstants.RelativeTime.MinuteAgo);
    private static readonly CompositeFormat HourAgoFormat = CompositeFormat.Parse(DomainConstants.RelativeTime.HourAgo);
    private static readonly CompositeFormat DayAgoFormat = CompositeFormat.Parse(DomainConstants.RelativeTime.DayAgo);
    private static readonly CompositeFormat WeekAgoFormat = CompositeFormat.Parse(DomainConstants.RelativeTime.WeekAgo);
    private static readonly CompositeFormat MonthAgoFormat = CompositeFormat.Parse(DomainConstants.RelativeTime.MonthAgo);
    private static readonly CompositeFormat YearAgoFormat = CompositeFormat.Parse(DomainConstants.RelativeTime.YearAgo);

    // ── Conversion ──────────────────────────────────────────────────

    /// <summary>Converts to the user's local time zone.</summary>
    public static DateTimeOffset ToUserLocalTime(this DateTimeOffset value, TimeZoneInfo userTimeZone)
        => TimeZoneInfo.ConvertTime(value, userTimeZone);

    // ── Formatting (with explicit TimeZoneInfo) ─────────────────────

    /// <summary>Formats as date only (e.g. "2026-04-25") in the user's time zone.</summary>
    public static string FormatAsDateOnly(this DateTimeOffset value, TimeZoneInfo userTimeZone)
        => value.ToUserLocalTime(userTimeZone)
               .ToString(DomainConstants.DateTimeFormats.DateOnly, CultureInfo.InvariantCulture);

    /// <summary>Formats as date + time (e.g. "2026-04-25 14:30") in the user's time zone.</summary>
    public static string FormatAsDateTime(this DateTimeOffset value, TimeZoneInfo userTimeZone)
        => value.ToUserLocalTime(userTimeZone)
               .ToString(DomainConstants.DateTimeFormats.DateTime, CultureInfo.InvariantCulture);

    /// <summary>Formats as date + time with seconds (e.g. "2026-04-25 14:30:05") in the user's time zone.</summary>
    public static string FormatAsDateTimeFull(this DateTimeOffset value, TimeZoneInfo userTimeZone)
        => value.ToUserLocalTime(userTimeZone)
               .ToString(DomainConstants.DateTimeFormats.DateTimeFull, CultureInfo.InvariantCulture);

    /// <summary>Formats as time only (e.g. "14:30") in the user's time zone.</summary>
    public static string FormatAsTime(this DateTimeOffset value, TimeZoneInfo userTimeZone)
        => value.ToUserLocalTime(userTimeZone)
               .ToString(DomainConstants.DateTimeFormats.TimeOnly, CultureInfo.InvariantCulture);

    /// <summary>Formats as time with seconds (e.g. "14:30:05") in the user's time zone.</summary>
    public static string FormatAsTimeWithSeconds(this DateTimeOffset value, TimeZoneInfo userTimeZone)
        => value.ToUserLocalTime(userTimeZone)
               .ToString(DomainConstants.DateTimeFormats.TimeWithSeconds, CultureInfo.InvariantCulture);

    /// <summary>Formats as month + day (e.g. "Apr 25") in the user's time zone.</summary>
    public static string FormatAsMonthDay(this DateTimeOffset value, TimeZoneInfo userTimeZone)
        => value.ToUserLocalTime(userTimeZone)
               .ToString(DomainConstants.DateTimeFormats.MonthDay, CultureInfo.InvariantCulture);

    /// <summary>Formats as month + day + year (e.g. "Apr 25, 2026") in the user's time zone.</summary>
    public static string FormatAsMonthDayYear(this DateTimeOffset value, TimeZoneInfo userTimeZone)
        => value.ToUserLocalTime(userTimeZone)
               .ToString(DomainConstants.DateTimeFormats.MonthDayYear, CultureInfo.InvariantCulture);

    /// <summary>Formats with a custom format string, in the user's time zone.</summary>
    public static string FormatAs(this DateTimeOffset value, TimeZoneInfo userTimeZone, string format)
        => value.ToUserLocalTime(userTimeZone)
               .ToString(format, CultureInfo.InvariantCulture);

    // ── Relative Time ───────────────────────────────────────────────

    /// <summary>
    ///     Returns a human-readable relative time string (e.g. "5m ago", "2d ago")
    ///     based on the difference between the value and <see cref="DateTimeOffset.UtcNow"/>.
    /// </summary>
    public static string FormatAsRelative(this DateTimeOffset value)
    {
        var elapsed = DateTimeOffset.UtcNow - value;
        var totalSeconds = (long)elapsed.TotalSeconds;

        if (totalSeconds < 0)
            return DomainConstants.RelativeTime.JustNow;

        return totalSeconds switch
        {
            < DomainConstants.RelativeTime.SecondsPerMinute
                => DomainConstants.RelativeTime.JustNow,

            < DomainConstants.RelativeTime.SecondsPerHour
                => string.Format(CultureInfo.InvariantCulture,
                    MinuteAgoFormat, totalSeconds / DomainConstants.RelativeTime.SecondsPerMinute),

            < DomainConstants.RelativeTime.SecondsPerDay
                => string.Format(CultureInfo.InvariantCulture,
                    HourAgoFormat, totalSeconds / DomainConstants.RelativeTime.SecondsPerHour),

            _ => FormatRelativeDays(elapsed.Days)
        };
    }

    private static string FormatRelativeDays(int days) => days switch
    {
        < DomainConstants.RelativeTime.DaysPerWeek
            => string.Format(CultureInfo.InvariantCulture, DayAgoFormat, days),

        < DomainConstants.RelativeTime.DaysPerMonth
            => string.Format(CultureInfo.InvariantCulture, WeekAgoFormat, days / DomainConstants.RelativeTime.DaysPerWeek),

        < DomainConstants.RelativeTime.DaysPerYear
            => string.Format(CultureInfo.InvariantCulture, MonthAgoFormat, days / DomainConstants.RelativeTime.DaysPerMonth),

        _ => string.Format(CultureInfo.InvariantCulture, YearAgoFormat, days / DomainConstants.RelativeTime.DaysPerYear)
    };
}
