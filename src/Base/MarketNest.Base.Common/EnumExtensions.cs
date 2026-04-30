using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace MarketNest.Base.Common;

/// <summary>
///     Extension methods for enum types. Provides <c>[Description]</c> attribute support,
///     safe parsing, and utility methods.
/// </summary>
public static class EnumExtensions
{
    // Cache description lookups to avoid repeated reflection — keyed by enum value
    private static readonly ConcurrentDictionary<Enum, string> DescriptionCache = new();

    // ── [Description] Attribute ─────────────────────────────────────

    /// <summary>
    ///     Returns the <see cref="DescriptionAttribute"/> value for the enum member,
    ///     or the enum member name (ToString) if no attribute is present.
    /// </summary>
    /// <example>
    ///     <code>OrderStatus.PendingPayment.ToDescription() // "Pending Payment"</code>
    /// </example>
    public static string ToDescription(this Enum value)
        => DescriptionCache.GetOrAdd(value, static v =>
        {
            FieldInfo? field = v.GetType().GetField(v.ToString());
            if (field is null) return v.ToString();

            DescriptionAttribute? attribute = field.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? v.ToString();
        });

    /// <summary>
    ///     Parses an enum value from its <see cref="DescriptionAttribute"/> text.
    ///     Returns null if no matching description is found.
    /// </summary>
    /// <typeparam name="T">The enum type to search.</typeparam>
    /// <example>
    ///     <code>EnumExtensions.FromDescription&lt;OrderStatus&gt;("Pending Payment") // OrderStatus.PendingPayment</code>
    /// </example>
    public static T? FromDescription<T>(string description) where T : struct, Enum
    {
        foreach (T value in Enum.GetValues<T>())
        {
            if (string.Equals(((Enum)(object)value).ToDescription(), description, StringComparison.OrdinalIgnoreCase))
                return value;
        }

        return null;
    }

    /// <summary>
    ///     Parses an enum value from its <see cref="DescriptionAttribute"/> text.
    ///     Throws <see cref="ArgumentException"/> if no matching description is found.
    /// </summary>
    public static T FromDescriptionRequired<T>(string description) where T : struct, Enum
        => FromDescription<T>(description)
           ?? throw new ArgumentException($"No enum value of type '{typeof(T).Name}' has description '{description}'.");

    // ── Safe Parsing ────────────────────────────────────────────────

    /// <summary>
    ///     Attempts to parse a string to the enum type. Case-insensitive.
    ///     Returns null if parsing fails.
    /// </summary>
    public static T? ParseOrNull<T>(string? value) where T : struct, Enum
        => Enum.TryParse<T>(value, ignoreCase: true, out T result) ? result : null;

    /// <summary>
    ///     Parses a string to the enum type. Case-insensitive.
    ///     Returns <paramref name="defaultValue"/> if parsing fails.
    /// </summary>
    public static T ParseOrDefault<T>(string? value, T defaultValue) where T : struct, Enum
        => Enum.TryParse<T>(value, ignoreCase: true, out T result) ? result : defaultValue;

    // ── Utility ─────────────────────────────────────────────────────

    /// <summary>Returns all defined values for enum type <typeparamref name="T"/>.</summary>
    public static IReadOnlyList<T> GetValues<T>() where T : struct, Enum
        => Enum.GetValues<T>();

    /// <summary>Returns all enum values paired with their description text.</summary>
    public static IReadOnlyList<(T Value, string Description)> GetValuesWithDescriptions<T>() where T : struct, Enum
    {
        T[] values = Enum.GetValues<T>();
        var result = new List<(T, string)>(values.Length);
        foreach (T value in values)
        {
            result.Add((value, ((Enum)(object)value).ToDescription()));
        }

        return result;
    }

    /// <summary>Returns true if the value is a defined member of the enum.</summary>
    public static bool IsDefined<T>(this T value) where T : struct, Enum
        => Enum.IsDefined(value);

    /// <summary>Returns true if the integer value maps to a defined enum member.</summary>
    public static bool IsDefined<T>(int value) where T : struct, Enum
        => Enum.IsDefined(typeof(T), value);
}

