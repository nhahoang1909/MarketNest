using System.Globalization;
using MarketNest.Base.Infrastructure;
using Microsoft.Extensions.Localization;

namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Wraps <see cref="IStringLocalizer{SharedResource}" /> — the entire project only knows II18NService.
///     Missing key: logs Warning + returns <see cref="string.Empty" /> (never crashes the app).
/// </summary>
internal sealed partial class I18NService(
    IStringLocalizer<SharedResource> localizer,
    IAppLogger<I18NService> logger) : II18NService
{
    public string this[string key] => GetString(key);

    public string Get(string key, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var result = localizer[key];

        if (result.ResourceNotFound)
        {
            Log.KeyNotFound(logger, key, CultureInfo.CurrentUICulture.Name);
            return string.Empty;
        }

        if (args.Length == 0)
            return result.Value;

        try
        {
            return string.Format(CultureInfo.CurrentCulture, result.Value, args);
        }
        catch (FormatException ex)
        {
            Log.FormatError(logger, key, ex);
            return result.Value;
        }
    }

    public bool KeyExists(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return !localizer[key].ResourceNotFound;
    }

    private string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var result = localizer[key];

        if (result.ResourceNotFound)
        {
            Log.KeyNotFound(logger, key, CultureInfo.CurrentUICulture.Name);
            return string.Empty;
        }

        return result.Value;
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.I18NKeyNotFound, LogLevel.Warning,
            "I18N key not found — Key={I18NKey} Culture={Culture}")]
        public static partial void KeyNotFound(ILogger logger, string i18NKey, string culture);

        [LoggerMessage((int)LogEventId.I18NFormatError, LogLevel.Warning,
            "I18N format error — Key={I18NKey}")]
        public static partial void FormatError(ILogger logger, string i18NKey, Exception ex);
    }
}

