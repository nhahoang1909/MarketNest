using MarketNest.Base.Common;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Resolves the current user's time zone and date format preferences from HTTP context.
///     Falls back to UTC and ISO formats when no preference is available.
///     Future: read from authenticated user's profile claims or a "tz" cookie
///     set by the browser (Intl.DateTimeFormat().resolvedOptions().timeZone).
/// </summary>
public sealed class HttpContextUserTimeZoneProvider(IHttpContextAccessor httpContextAccessor) : IUserTimeZoneProvider
{
    private const string TimeZoneCookieName = ".MarketNest.TimeZone";
    private const string DateFormatCookieName = ".MarketNest.DateFormat";
    private const string DateTimeFormatCookieName = ".MarketNest.DateTimeFormat";
    private const string TimeFormatCookieName = ".MarketNest.TimeFormat";

    public TimeZoneInfo TimeZone => ResolveTimeZone();

    public string DateFormat =>
        ReadCookie(DateFormatCookieName) ?? DomainConstants.DateTimeFormats.DateOnly;

    public string DateTimeFormat =>
        ReadCookie(DateTimeFormatCookieName) ?? DomainConstants.DateTimeFormats.DateTime;

    public string TimeFormat =>
        ReadCookie(TimeFormatCookieName) ?? DomainConstants.DateTimeFormats.TimeOnly;

    private TimeZoneInfo ResolveTimeZone()
    {
        string? tzId = ReadCookie(TimeZoneCookieName);

        if (string.IsNullOrWhiteSpace(tzId))
            return TimeZoneInfo.Utc;

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private string? ReadCookie(string name)
        => httpContextAccessor.HttpContext?.Request.Cookies[name];
}
