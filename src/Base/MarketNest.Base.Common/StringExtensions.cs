using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MarketNest.Base.Common;

/// <summary>
///     Common string extension methods for truncation, null-coalescing,
///     slug generation, masking, and formatting.
/// </summary>
public static partial class StringExtensions
{
    private const string DefaultEllipsis = "…";
    private const char SlugSeparator = '-';

    // ── Null / Empty Checks (instance wrappers) ─────────────────────

    /// <summary>Returns true if the string is null or empty.</summary>
    public static bool IsNullOrEmpty(this string? value)
        => string.IsNullOrEmpty(value);

    /// <summary>Returns true if the string is null, empty, or only whitespace.</summary>
    public static bool IsNullOrWhiteSpace(this string? value)
        => string.IsNullOrWhiteSpace(value);

    /// <summary>Returns true if the string has a non-whitespace value.</summary>
    public static bool HasValue(this string? value)
        => !string.IsNullOrWhiteSpace(value);

    // ── Null Coalescing ─────────────────────────────────────────────

    /// <summary>Returns null if the string is empty, otherwise returns the original value.</summary>
    public static string? NullIfEmpty(this string? value)
        => string.IsNullOrEmpty(value) ? null : value;

    /// <summary>Returns null if the string is empty or whitespace-only.</summary>
    public static string? NullIfWhiteSpace(this string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Returns <paramref name="fallback"/> if the string is null/empty/whitespace.</summary>
    public static string OrDefault(this string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    // ── Truncation ──────────────────────────────────────────────────

    /// <summary>
    ///     Truncates the string to <paramref name="maxLength"/> characters,
    ///     appending <paramref name="suffix"/> if truncation occurs.
    /// </summary>
    /// <example>
    ///     <code>"Hello World".Truncate(8) // "Hello W…"</code>
    /// </example>
    public static string Truncate(this string? value, int maxLength, string suffix = DefaultEllipsis)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (maxLength <= 0) return string.Empty;
        if (value.Length <= maxLength) return value;

        int truncateAt = maxLength - suffix.Length;
        return truncateAt <= 0 ? value[..maxLength] : string.Concat(value.AsSpan(0, truncateAt), suffix);
    }

    // ── Slug Generation ─────────────────────────────────────────────

    /// <summary>
    ///     Converts a string to a URL-friendly slug (lowercase, hyphens, no special chars).
    ///     Removes diacritics, replaces spaces/underscores with hyphens, collapses multiple hyphens.
    /// </summary>
    /// <example>
    ///     <code>"Áo Dài Truyền Thống".ToSlug() // "ao-dai-truyen-thong"</code>
    /// </example>
    public static string ToSlug(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        // Normalize and remove diacritics
        string normalized = value.RemoveDiacritics();

        // Lowercase
        normalized = normalized.ToLowerInvariant();

        // Replace non-alphanumeric with hyphens
        normalized = NonAlphanumericRegex().Replace(normalized, SlugSeparator.ToString());

        // Collapse multiple hyphens
        normalized = MultipleHyphensRegex().Replace(normalized, SlugSeparator.ToString());

        // Trim leading/trailing hyphens
        return normalized.Trim(SlugSeparator);
    }

    // ── Diacritics ──────────────────────────────────────────────────

    /// <summary>
    ///     Removes diacritical marks (accents) from a string.
    ///     E.g., "café" → "cafe", "Nguyễn" → "Nguyen".
    /// </summary>
    public static string RemoveDiacritics(this string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        string normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);

        foreach (char c in normalized)
        {
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    // ── Masking ─────────────────────────────────────────────────────

    /// <summary>
    ///     Masks an email address for display (e.g., "john.doe@example.com" → "jo***@example.com").
    /// </summary>
    public static string MaskEmail(this string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;

        int atIndex = email.IndexOf('@', StringComparison.Ordinal);
        if (atIndex <= 0) return "***";

        int visibleChars = Math.Min(2, atIndex);
        string localPart = email[..visibleChars];
        string domain = email[atIndex..];
        return $"{localPart}***{domain}";
    }

    /// <summary>
    ///     Masks a phone number for display, showing only the last 4 digits.
    ///     E.g., "+84912345678" → "****5678".
    /// </summary>
    public static string MaskPhone(this string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

        // Keep only digits for length calculation
        string digitsOnly = DigitsOnlyRegex().Replace(phone, string.Empty);
        if (digitsOnly.Length <= 4) return "****";

        return $"****{digitsOnly[^4..]}";
    }

    // ── Casing ──────────────────────────────────────────────────────

    /// <summary>Converts "camelCase" or "PascalCase" to "Title Case" with spaces.</summary>
    public static string ToTitleCase(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        // Insert space before uppercase letters (except the first)
        string spaced = PascalCaseBoundaryRegex().Replace(value, " $1");
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced.ToLowerInvariant());
    }

    /// <summary>Converts "PascalCase" or "Title Case" to "snake_case".</summary>
    public static string ToSnakeCase(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        string spaced = PascalCaseBoundaryRegex().Replace(value, "_$1");
        return spaced.Trim('_').ToLowerInvariant().Replace(' ', '_');
    }

    // ── Content Utilities ───────────────────────────────────────────

    /// <summary>
    ///     Returns the first <paramref name="wordCount"/> words from the string.
    ///     Useful for generating excerpts from descriptions.
    /// </summary>
    public static string FirstWords(this string? value, int wordCount, string suffix = DefaultEllipsis)
    {
        if (string.IsNullOrWhiteSpace(value) || wordCount <= 0) return string.Empty;

        string[] words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= wordCount) return value.Trim();

        return string.Join(' ', words[..wordCount]) + suffix;
    }

    /// <summary>
    ///     Removes all HTML tags from the string. Useful for sanitizing rich text for plain display.
    /// </summary>
    public static string StripHtml(this string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return HtmlTagRegex().Replace(value, string.Empty).Trim();
    }

    // ── Validation Checks ───────────────────────────────────────────

    /// <summary>
    ///     Returns true if the string is a valid email address.
    ///     Uses a basic pattern check — for production use, consider FluentValidation's EmailAddress() validator.
    /// </summary>
    public static bool IsValidEmail(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Length > FieldLimits.Email.MaxLength) return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(value);
            return addr.Address == value;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Returns true if the string is a valid E.164 phone number (e.g., "+84912345678").
    ///     Matches pattern: +[country code][number], 8-16 characters total.
    /// </summary>
    public static bool IsValidPhoneNumber(this string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.Length <= FieldLimits.PhoneNumber.MaxLength
           && PhoneNumberRegex().IsMatch(value);

    /// <summary>
    ///     Returns true if the string is a valid slug (lowercase alphanumeric + hyphens, 3-50 chars).
    /// </summary>
    public static bool IsValidSlug(this string? value)
        => !string.IsNullOrWhiteSpace(value)
           && SlugRegex().IsMatch(value);

    /// <summary>
    ///     Returns true if the string is a valid HTTP(S) URL.
    /// </summary>
    public static bool IsValidUrl(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (value.Length > FieldLimits.Url.MaxLength) return false;

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    /// <summary>
    ///     Returns true if the string is a valid ISO 3166-1 alpha-2 country code (e.g., "US", "VN").
    /// </summary>
    public static bool IsValidCountryCode(this string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.Length == FieldLimits.CountryCode.ExactLength
           && CountryCodeRegex().IsMatch(value);

    /// <summary>
    ///     Returns true if the string is a valid ISO 4217 currency code (e.g., "USD", "VND").
    /// </summary>
    public static bool IsValidCurrencyCode(this string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.Length == FieldLimits.CurrencyCode.ExactLength
           && CurrencyCodeRegex().IsMatch(value);

    /// <summary>
    ///     Returns true if the string is a valid postal/ZIP code (3-20 alphanumeric + space/hyphen).
    /// </summary>
    public static bool IsValidPostalCode(this string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.Length is >= FieldLimits.PostalCode.MinLength and <= FieldLimits.PostalCode.MaxLength
           && PostalCodeRegex().IsMatch(value);

    // ── Character Checks (useful for password validation) ───────────

    /// <summary>Returns true if the string contains only digits (0-9).</summary>
    public static bool IsNumeric(this string? value)
        => !string.IsNullOrWhiteSpace(value) && NumericOnlyRegex().IsMatch(value);

    /// <summary>Returns true if the string contains only alphanumeric characters (a-z, A-Z, 0-9).</summary>
    public static bool IsAlphanumeric(this string? value)
        => !string.IsNullOrWhiteSpace(value) && AlphanumericOnlyRegex().IsMatch(value);

    /// <summary>Returns true if the string contains at least one digit.</summary>
    public static bool ContainsDigit(this string? value)
        => !string.IsNullOrWhiteSpace(value) && HasDigitRegex().IsMatch(value);

    /// <summary>Returns true if the string contains at least one uppercase letter (A-Z).</summary>
    public static bool ContainsUpperCase(this string? value)
        => !string.IsNullOrWhiteSpace(value) && HasUpperCaseRegex().IsMatch(value);

    /// <summary>Returns true if the string contains at least one lowercase letter (a-z).</summary>
    public static bool ContainsLowerCase(this string? value)
        => !string.IsNullOrWhiteSpace(value) && HasLowerCaseRegex().IsMatch(value);

    /// <summary>Returns true if the string contains at least one special character (non-alphanumeric).</summary>
    public static bool ContainsSpecialChar(this string? value)
        => !string.IsNullOrWhiteSpace(value) && HasSpecialCharRegex().IsMatch(value);

    /// <summary>
    ///     Returns true if the string meets basic password strength requirements:
    ///     at least 8 characters, contains upper, lower, digit, and special char.
    /// </summary>
    public static bool IsStrongPassword(this string? value)
        => !string.IsNullOrWhiteSpace(value)
           && value.Length >= 8
           && value.ContainsUpperCase()
           && value.ContainsLowerCase()
           && value.ContainsDigit()
           && value.ContainsSpecialChar();

    // ── Regex Source Generators ─────────────────────────────────────

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultipleHyphensRegex();

    [GeneratedRegex(@"\D")]
    private static partial Regex DigitsOnlyRegex();

    [GeneratedRegex(@"(?<!^)([A-Z])")]
    private static partial Regex PascalCaseBoundaryRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    // Validation patterns (from FieldLimits)
    [GeneratedRegex(@"^\+[1-9]\d{1,14}$")]
    private static partial Regex PhoneNumberRegex();

    [GeneratedRegex(@"^[a-z0-9-]{3,50}$")]
    private static partial Regex SlugRegex();

    [GeneratedRegex(@"^[A-Z]{2}$")]
    private static partial Regex CountryCodeRegex();

    [GeneratedRegex(@"^[A-Z]{3}$")]
    private static partial Regex CurrencyCodeRegex();

    [GeneratedRegex(@"^[A-Z0-9\s-]{3,20}$")]
    private static partial Regex PostalCodeRegex();

    // Character class checks
    [GeneratedRegex(@"^\d+$")]
    private static partial Regex NumericOnlyRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9]+$")]
    private static partial Regex AlphanumericOnlyRegex();

    [GeneratedRegex(@"\d")]
    private static partial Regex HasDigitRegex();

    [GeneratedRegex(@"[A-Z]")]
    private static partial Regex HasUpperCaseRegex();

    [GeneratedRegex(@"[a-z]")]
    private static partial Regex HasLowerCaseRegex();

    [GeneratedRegex(@"\W")]
    private static partial Regex HasSpecialCharRegex();
}

