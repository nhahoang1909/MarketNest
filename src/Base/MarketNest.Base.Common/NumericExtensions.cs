using System.Globalization;

namespace MarketNest.Base.Common;

/// <summary>
///     Common numeric extension methods for clamping, formatting, and range utilities.
/// </summary>
public static class NumericExtensions
{
    // ── Clamping ────────────────────────────────────────────────────

    /// <summary>Clamps the value between <paramref name="min"/> and <paramref name="max"/> (inclusive).</summary>
    public static int Clamp(this int value, int min, int max)
        => Math.Clamp(value, min, max);

    /// <summary>Clamps the value between <paramref name="min"/> and <paramref name="max"/> (inclusive).</summary>
    public static decimal Clamp(this decimal value, decimal min, decimal max)
        => Math.Clamp(value, min, max);

    // ── Range Checks ────────────────────────────────────────────────

    /// <summary>Returns true if the value is within the inclusive range [min, max].</summary>
    public static bool IsBetween(this int value, int min, int max)
        => value >= min && value <= max;

    /// <summary>Returns true if the value is within the inclusive range [min, max].</summary>
    public static bool IsBetween(this decimal value, decimal min, decimal max)
        => value >= min && value <= max;

    /// <summary>Returns true if the value is positive (greater than zero).</summary>
    public static bool IsPositive(this int value) => value > 0;

    /// <summary>Returns true if the value is positive (greater than zero).</summary>
    public static bool IsPositive(this decimal value) => value > 0m;

    /// <summary>Returns true if the value is zero or positive.</summary>
    public static bool IsNonNegative(this int value) => value >= 0;

    /// <summary>Returns true if the value is zero or positive.</summary>
    public static bool IsNonNegative(this decimal value) => value >= 0m;

    // ── Formatting ──────────────────────────────────────────────────

    /// <summary>
    ///     Formats as a compact human-readable number (e.g., 1500 → "1.5K", 2300000 → "2.3M").
    /// </summary>
    public static string ToCompactString(this int value) => value switch
    {
        >= 1_000_000_000 => $"{value / 1_000_000_000.0:0.#}B",
        >= 1_000_000 => $"{value / 1_000_000.0:0.#}M",
        >= 1_000 => $"{value / 1_000.0:0.#}K",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>
    ///     Formats as a compact human-readable number (e.g., 1500 → "1.5K", 2300000 → "2.3M").
    /// </summary>
    public static string ToCompactString(this long value) => value switch
    {
        >= 1_000_000_000 => $"{value / 1_000_000_000.0:0.#}B",
        >= 1_000_000 => $"{value / 1_000_000.0:0.#}M",
        >= 1_000 => $"{value / 1_000.0:0.#}K",
        _ => value.ToString(CultureInfo.InvariantCulture)
    };

    /// <summary>
    ///     Formats a decimal as currency with thousand separators (no currency symbol).
    ///     E.g., 1234567.89 → "1,234,567.89".
    /// </summary>
    public static string ToFormattedNumber(this decimal value, int decimalPlaces = 2)
        => value.ToString($"N{decimalPlaces}", CultureInfo.InvariantCulture);

    /// <summary>
    ///     Formats an integer with thousand separators.
    ///     E.g., 1234567 → "1,234,567".
    /// </summary>
    public static string ToFormattedNumber(this int value)
        => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>
    ///     Formats as a percentage string (e.g., 0.156m → "15.6%", 85 → "85%").
    /// </summary>
    public static string ToPercentageString(this decimal value, int decimalPlaces = 1)
    {
        // If value is between 0 and 1, treat as a ratio
        decimal displayValue = value is > 0m and <= 1m ? value * 100 : value;
        return $"{displayValue.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture)}%";
    }

    // ── Ordinal ─────────────────────────────────────────────────────

    /// <summary>
    ///     Returns the ordinal suffix for a number (e.g., 1 → "1st", 2 → "2nd", 3 → "3rd", 4 → "4th").
    /// </summary>
    public static string ToOrdinal(this int value)
    {
        // Special case for teens (11th, 12th, 13th)
        int lastTwoDigits = Math.Abs(value % 100);
        if (lastTwoDigits is >= 11 and <= 13)
            return $"{value}th";

        int lastDigit = Math.Abs(value % 10);
        string suffix = lastDigit switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };

        return $"{value}{suffix}";
    }

    // ── Byte Size Formatting ────────────────────────────────────────

    /// <summary>
    ///     Formats a byte count as a human-readable file size string.
    ///     E.g., 1536 → "1.5 KB", 1048576 → "1 MB".
    /// </summary>
    public static string ToFileSize(this long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (1024.0 * 1024.0 * 1024.0):0.##} GB",
        >= 1L << 20 => $"{bytes / (1024.0 * 1024.0):0.##} MB",
        >= 1L << 10 => $"{bytes / 1024.0:0.##} KB",
        _ => $"{bytes} B"
    };

    /// <summary>
    ///     Formats a byte count as a human-readable file size string.
    /// </summary>
    public static string ToFileSize(this int bytes) => ((long)bytes).ToFileSize();
}

