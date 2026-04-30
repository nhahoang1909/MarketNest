namespace MarketNest.Web.Infrastructure;

/// <summary>
///     Single entry point for all localization in the project.
///     Inject this interface instead of IStringLocalizer&lt;T&gt; everywhere.
/// </summary>
public interface II18NService
{
    /// <summary>
    ///     Gets the localized string for the given key.
    ///     Returns <see cref="string.Empty" /> if the key is not found — never throws, never displays raw keys.
    /// </summary>
    string this[string key] { get; }

    /// <summary>
    ///     Gets the localized string with format arguments.
    ///     Example: Get("Welcome.Message", userName) with template "Hello, {0}!"
    ///     Returns <see cref="string.Empty" /> if the key is not found.
    /// </summary>
    string Get(string key, params object[] args);

    /// <summary>
    ///     Checks whether a key exists in the current culture's resource.
    /// </summary>
    bool KeyExists(string key);
}

