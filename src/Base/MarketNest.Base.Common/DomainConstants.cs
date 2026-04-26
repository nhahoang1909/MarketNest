namespace MarketNest.Core.Common;

/// <summary>
/// Domain-wide constants. Every magic number or repeated string literal
/// from domain logic must be defined here.
/// </summary>
public static class DomainConstants
{
    // ── Pagination ───────────────────────────────────────────────────
    public static class Pagination
    {
        public const int DefaultPageSize = 20;
        public const int MinPageSize = 1;
        public const int MaxPageSize = 100;
        public const int MinPage = 1;
    }

    // ── Validation Limits ────────────────────────────────────────────
    public static class Validation
    {
        public const int SlugMinLength = 3;
        public const int SlugMaxLength = 50;
        public const string SlugPattern = @"^[a-z0-9-]{3,50}$";
        public const string SlugErrorMessage = "Must be 3-50 lowercase letters, numbers, or hyphens";

        public const decimal MaxMoneyAmount = 999_999.99m;
        public const string MoneyPositiveMessage = "Amount must be greater than 0";
        public const string MoneyMaxMessage = "Amount exceeds maximum allowed value";

        public const int MaxEmailLength = 254;

        public const int MinQuantity = 1;
        public const int MaxQuantity = 99;
        public const string QuantityRangeMessage = "Quantity must be between 1 and 99";

        public const string IdEmptyMessage = "ID cannot be empty";
    }

    // ── Date & Time Formats ─────────────────────────────────────────
    public static class DateTimeFormats
    {
        public const string DateOnly = "yyyy-MM-dd";
        public const string DateTime = "yyyy-MM-dd HH:mm";
        public const string DateTimeFull = "yyyy-MM-dd HH:mm:ss";
        public const string TimeOnly = "HH:mm";
        public const string TimeWithSeconds = "HH:mm:ss";
        public const string MonthDay = "MMM dd";
        public const string MonthDayYear = "MMM dd, yyyy";
    }

    public static class RelativeTime
    {
        public const int SecondsPerMinute = 60;
        public const int SecondsPerHour = 3_600;
        public const int SecondsPerDay = 86_400;
        public const int DaysPerWeek = 7;
        public const int DaysPerMonth = 30;
        public const int DaysPerYear = 365;

        // Labels (English) — localize via resource files for user-facing display
        public const string JustNow = "just now";
        public const string SecondAgo = "{0}s ago";
        public const string MinuteAgo = "{0}m ago";
        public const string HourAgo = "{0}h ago";
        public const string DayAgo = "{0}d ago";
        public const string WeekAgo = "{0}w ago";
        public const string MonthAgo = "{0}mo ago";
        public const string YearAgo = "{0}y ago";
    }

    // ── Error Codes ──────────────────────────────────────────────────
    public static class ErrorCodes
    {
        public const string Unauthorized = "UNAUTHORIZED";
        public const string Forbidden = "FORBIDDEN";
        public const string UnexpectedError = "UNEXPECTED_ERROR";
        public const string NotFoundSuffix = "NOT_FOUND";
    }

    // ── Error Messages ───────────────────────────────────────────────
    public static class ErrorMessages
    {
        public const string AuthenticationRequired = "Authentication required";
        public const string InsufficientPermissions = "Insufficient permissions";
        public const string UnexpectedError = "An unexpected error occurred";
        public const string PageMustBePositive = "Page must be >= 1";
        public const string PageSizeRange = "PageSize must be between 1 and 100";
    }
}

