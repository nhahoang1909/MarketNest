using System.Reflection;

namespace MarketNest.Base.Common;

/// <summary>
///     Extension methods to convert strongly-typed variable records to Dictionary for template rendering.
/// </summary>
public static class NotificationVariableExtensions
{
    /// <summary>
    ///     Converts any notification variable record to a string dictionary
    ///     using reflection on public properties.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ToVariables<T>(this T variables) where T : class
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (PropertyInfo prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var value = prop.GetValue(variables)?.ToString() ?? string.Empty;
            dict[prop.Name] = value;
        }
        return dict;
    }
}

