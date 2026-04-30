namespace MarketNest.Base.Common;

/// <summary>
///     Single source of truth for all field validation limits across the project.
///     Referenced by FluentValidation validators, EF Core configurations, and Razor views.
/// </summary>
public static class FieldLimits
{
    // ── String Tiers ─────────────────────────────────────────────────────

    /// <summary>Code, slug, SKU, short key — max 50 characters.</summary>
    public static class Identifier
    {
        public const int MaxLength = 50;
        public const int MinLength = 2;
    }

    /// <summary>Short names, labels — max 100 characters.</summary>
    public static class InlineShort
    {
        public const int MaxLength = 100;
    }

    /// <summary>Entity names — max 255 characters.</summary>
    public static class InlineStandard
    {
        public const int MaxLength = 255;
    }

    /// <summary>Bio, tagline, short description — max 500 characters.</summary>
    public static class InlineExtended
    {
        public const int MaxLength = 500;
    }

    /// <summary>Notes, comments — max 2000 characters.</summary>
    public static class MultilineStandard
    {
        public const int MaxLength = 2000;
    }

    /// <summary>Short descriptions, templates — max 5000 characters.</summary>
    public static class MultilineLong
    {
        public const int MaxLength = 5000;
    }

    /// <summary>Full rich-text descriptions — max 20000 characters.</summary>
    public static class MultilineDocument
    {
        public const int MaxLength = 20000;
    }

    // ── Special Format Fields ────────────────────────────────────────────

    public static class Email
    {
        public const int MaxLength = 254; // RFC 5321
    }

    public static class Url
    {
        public const int MaxLength = 500;
    }

    public static class PostalCode
    {
        public const int MinLength = 3;
        public const int MaxLength = 20;
        public const string Pattern = @"^[A-Z0-9\s-]{3,20}$";
    }

    public static class CountryCode
    {
        public const int ExactLength = 2; // ISO 3166-1 alpha-2
        public const string Pattern = @"^[A-Z]{2}$";
    }

    public static class CurrencyCode
    {
        public const int ExactLength = 3; // ISO 4217
        public const string Pattern = @"^[A-Z]{3}$";
    }

    public static class PhoneNumber
    {
        public const int MaxLength = 16; // E.164
        public const string Pattern = @"^\+[1-9]\d{1,14}$";
    }

    public static class Slug
    {
        public const int MinLength = 3;
        public const int MaxLength = 50;
        public const string Pattern = @"^[a-z0-9-]{3,50}$";
    }

    public static class Sku
    {
        public const int MinLength = 2;
        public const int MaxLength = 50;
    }

    // ── Numeric Limits ───────────────────────────────────────────────────

    public static class Money
    {
        public const decimal Min = 0.01m;
        public const decimal Max = 999_999.99m;
        public const int DecimalPlaces = 2;
    }

    public static class Percentage
    {
        public const decimal Min = 0m;
        public const decimal Max = 100m;
        public const int DecimalPlaces = 4;
    }

    public static class Quantity
    {
        public const int CartItemMin = 1;
        public const int CartItemMax = 99;
        public const int StockMin = 0;
        public const int StockMax = 999_999;
        public const int LowStockThresholdMax = 999;
    }

    public static class Weight
    {
        public const decimal Min = 0.001m;
        public const decimal Max = 999.999m;
        public const int DecimalPlaces = 3;
    }

    public static class Rating
    {
        public const int Min = 1;
        public const int Max = 5;
    }

    // ── Pagination ───────────────────────────────────────────────────────

    public static class Pagination
    {
        public const int PageNumberMin = 1;
        public const int PageSizeMin = 1;
        public const int PageSizeMax = 100;
    }

    // ── Collections ──────────────────────────────────────────────────────

    public static class Collections
    {
        public const int MaxProductImages = 10;
        public const int MaxVariantsPerProduct = 50;
        public const int MaxTagsPerProduct = 20;
        public const int MaxCartItems = 50;
        public const int MaxWishlistItems = 200;
        public const int TagMaxLength = 50;
    }

    // ── File Upload ──────────────────────────────────────────────────────

    public static class FileUpload
    {
        public const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB
        public const long MaxExcelSizeBytes = 10 * 1024 * 1024; // 10 MB

        public static readonly string[] AllowedImageTypes =
            ["image/jpeg", "image/png", "image/webp"];

        public static readonly string[] AllowedExcelTypes =
        [
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.ms-excel"
        ];
    }

    // ── Excel Import ─────────────────────────────────────────────────────

    public static class ExcelImport
    {
        public const int MaxRowsPerBatch = 1000;
        public const int MaxErrorsReported = 100;
    }

    // ── Domain-Specific — reference tier constants above ─────────────────

    public static class Address
    {
        public const int RecipientNameMaxLength = InlineShort.MaxLength;       // 100
        public const int StreetMaxLength = 200;
        public const int CityMaxLength = InlineShort.MaxLength;               // 100
        public const int StateProvinceMaxLength = InlineShort.MaxLength;      // 100
    }

    public static class Product
    {
        public const int NameMaxLength = InlineStandard.MaxLength;            // 255
        public const int SlugMaxLength = Slug.MaxLength;                      // 50
        public const int ShortDescriptionMaxLength = MultilineLong.MaxLength; // 5000
        public const int DescriptionMaxLength = MultilineDocument.MaxLength;  // 20000
    }

    public static class Storefront
    {
        public const int NameMaxLength = InlineStandard.MaxLength;            // 255
        public const int SlugMaxLength = Slug.MaxLength;                      // 50
        public const int TaglineMaxLength = InlineExtended.MaxLength;         // 500
        public const int DescriptionMaxLength = MultilineDocument.MaxLength;  // 20000
    }

    public static class Review
    {
        public const int TitleMaxLength = InlineShort.MaxLength;              // 100
        public const int BodyMaxLength = InlineExtended.MaxLength;            // 500
        public const int SellerReplyMaxLength = InlineExtended.MaxLength;     // 500
        public const int EditableWindowHours = 48;
    }

    public static class Dispute
    {
        public const int MessageBodyMaxLength = MultilineStandard.MaxLength;  // 2000
        public const int SellerResponseHours = 72;
    }

    public static class Coupon
    {
        public const int CodeMaxLength = Identifier.MaxLength;                // 50
        public const int DescriptionMaxLength = InlineExtended.MaxLength;     // 500
        public const int MaxValidityDays = 730;                               // 2 years
    }

    public static class Notification
    {
        public const int TemplateSubjectMaxLength = InlineStandard.MaxLength; // 255
        // TemplateBody is Unbounded (admin-only content)
    }
}

