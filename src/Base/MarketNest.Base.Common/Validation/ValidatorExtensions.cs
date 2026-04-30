using System.Linq.Expressions;

using FluentValidation;

namespace MarketNest.Base.Common;

/// <summary>
///     Reusable FluentValidation rule extensions for common patterns.
///     All validators across the project should use these instead of inline rules.
/// </summary>
public static class ValidatorExtensions
{
    // ── Slug ──────────────────────────────────────────────────────────────

    public static IRuleBuilderOptions<T, string> MustBeSlug<T>(
        this IRuleBuilder<T, string> rule, string fieldName = "Slug")
        => rule
            .NotEmpty().WithMessage(ValidationMessages.Required(fieldName))
            .MaximumLength(FieldLimits.Slug.MaxLength)
            .WithMessage(ValidationMessages.MaxLength(fieldName, FieldLimits.Slug.MaxLength))
            .Matches(FieldLimits.Slug.Pattern)
            .WithMessage(ValidationMessages.InvalidSlugFormat(fieldName));

    // ── Email ─────────────────────────────────────────────────────────────

    public static IRuleBuilderOptions<T, string> MustBeValidEmail<T>(
        this IRuleBuilder<T, string> rule, string fieldName = "Email")
        => rule
            .NotEmpty().WithMessage(ValidationMessages.Required(fieldName))
            .EmailAddress().WithMessage(ValidationMessages.InvalidEmailFormat())
            .MaximumLength(FieldLimits.Email.MaxLength)
            .WithMessage(ValidationMessages.MaxLength(fieldName, FieldLimits.Email.MaxLength));

    // ── Phone (E.164) ─────────────────────────────────────────────────────

    public static IRuleBuilderOptions<T, string?> MustBeValidPhone<T>(
        this IRuleBuilder<T, string?> rule)
        => rule
            .Matches(FieldLimits.PhoneNumber.Pattern)
            .WithMessage(ValidationMessages.InvalidPhoneFormat());

    // ── Money ─────────────────────────────────────────────────────────────

    public static IRuleBuilderOptions<T, decimal> MustBePositiveMoney<T>(
        this IRuleBuilder<T, decimal> rule, string fieldName = "Price")
        => rule
            .GreaterThan(0).WithMessage(ValidationMessages.MustBePositive(fieldName))
            .LessThanOrEqualTo(FieldLimits.Money.Max)
            .WithMessage(ValidationMessages.MaxValue(fieldName, "999,999.99"))
            .Must(v => decimal.Round(v, FieldLimits.Money.DecimalPlaces) == v)
            .WithMessage(ValidationMessages.MaxDecimalPlaces(fieldName, FieldLimits.Money.DecimalPlaces));

    public static IRuleBuilderOptions<T, decimal> MustBeNonNegativeMoney<T>(
        this IRuleBuilder<T, decimal> rule, string fieldName = "Amount")
        => rule
            .GreaterThanOrEqualTo(0).WithMessage(ValidationMessages.MinValue(fieldName, 0))
            .LessThanOrEqualTo(FieldLimits.Money.Max)
            .WithMessage(ValidationMessages.MaxValue(fieldName, "999,999.99"))
            .Must(v => decimal.Round(v, FieldLimits.Money.DecimalPlaces) == v)
            .WithMessage(ValidationMessages.MaxDecimalPlaces(fieldName, FieldLimits.Money.DecimalPlaces));

    // ── Quantity ──────────────────────────────────────────────────────────

    public static IRuleBuilderOptions<T, int> MustBeValidQuantity<T>(
        this IRuleBuilder<T, int> rule, string fieldName = "Quantity")
        => rule
            .InclusiveBetween(FieldLimits.Quantity.CartItemMin, FieldLimits.Quantity.CartItemMax)
            .WithMessage(ValidationMessages.RangeBetween(fieldName,
                FieldLimits.Quantity.CartItemMin, FieldLimits.Quantity.CartItemMax));

    public static IRuleBuilderOptions<T, int> MustBeValidStockQuantity<T>(
        this IRuleBuilder<T, int> rule, string fieldName = "Stock quantity")
        => rule
            .GreaterThanOrEqualTo(FieldLimits.Quantity.StockMin)
            .WithMessage(ValidationMessages.MinValue(fieldName, FieldLimits.Quantity.StockMin))
            .LessThanOrEqualTo(FieldLimits.Quantity.StockMax)
            .WithMessage(ValidationMessages.MaxValue(fieldName, "999,999"));

    // ── Percentage ────────────────────────────────────────────────────────

    public static IRuleBuilderOptions<T, decimal> MustBeValidPercentage<T>(
        this IRuleBuilder<T, decimal> rule, string fieldName = "Percentage")
        => rule
            .InclusiveBetween(FieldLimits.Percentage.Min, FieldLimits.Percentage.Max)
            .WithMessage(ValidationMessages.RangeBetween(fieldName, "0", "100"))
            .Must(v => decimal.Round(v, FieldLimits.Percentage.DecimalPlaces) == v)
            .WithMessage(ValidationMessages.MaxDecimalPlaces(fieldName, FieldLimits.Percentage.DecimalPlaces));

    // ── GUID ──────────────────────────────────────────────────────────────

    public static IRuleBuilderOptions<T, Guid> MustBeValidId<T>(
        this IRuleBuilder<T, Guid> rule, string fieldName = "ID")
        => rule
            .NotEqual(Guid.Empty)
            .WithMessage(ValidationMessages.InvalidId(fieldName));

    // ── Country Code ──────────────────────────────────────────────────────

    public static IRuleBuilderOptions<T, string> MustBeValidCountryCode<T>(
        this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty().WithMessage(ValidationMessages.Required("Country code"))
            .Length(FieldLimits.CountryCode.ExactLength)
            .WithMessage(ValidationMessages.ExactLength("Country code", FieldLimits.CountryCode.ExactLength))
            .Matches(FieldLimits.CountryCode.Pattern)
            .WithMessage(ValidationMessages.InvalidFormat("Country code", "ISO 3166-1 alpha-2 (e.g., US, VN)"));

    // ── Currency Code ─────────────────────────────────────────────────────

    public static IRuleBuilderOptions<T, string> MustBeValidCurrencyCode<T>(
        this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty().WithMessage(ValidationMessages.Required("Currency"))
            .Length(FieldLimits.CurrencyCode.ExactLength)
            .WithMessage(ValidationMessages.ExactLength("Currency", FieldLimits.CurrencyCode.ExactLength))
            .Matches(FieldLimits.CurrencyCode.Pattern)
            .WithMessage(ValidationMessages.InvalidFormat("Currency", "ISO 4217 code (e.g., USD, VND)"));

    // ── Timezone ──────────────────────────────────────────────────────────

    public static IRuleBuilderOptions<T, string> MustBeValidTimezone<T>(
        this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty().WithMessage(ValidationMessages.Required("Timezone"))
            .Must(tz =>
            {
                try
                {
                    TimeZoneInfo.FindSystemTimeZoneById(tz);
                    return true;
                }
                catch
                {
                    return false;
                }
            })
            .WithMessage(ValidationMessages.InvalidFormat("Timezone", "IANA timezone ID (e.g., Asia/Ho_Chi_Minh)"));

    // ── URL ───────────────────────────────────────────────────────────────

    public static IRuleBuilderOptions<T, string> MustBeValidUrl<T>(
        this IRuleBuilder<T, string> rule, string fieldName = "URL")
        => rule
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var u)
                         && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp))
            .WithMessage(ValidationMessages.InvalidFormat(fieldName, "https://example.com"))
            .MaximumLength(FieldLimits.Url.MaxLength)
            .WithMessage(ValidationMessages.MaxLength(fieldName, FieldLimits.Url.MaxLength));

    // ── Postal Code ───────────────────────────────────────────────────────

    public static IRuleBuilderOptions<T, string> MustBeValidPostalCode<T>(
        this IRuleBuilder<T, string> rule)
        => rule
            .NotEmpty().WithMessage(ValidationMessages.Required("Postal code"))
            .MaximumLength(FieldLimits.PostalCode.MaxLength)
            .WithMessage(ValidationMessages.MaxLength("Postal code", FieldLimits.PostalCode.MaxLength))
            .Matches(FieldLimits.PostalCode.Pattern)
            .WithMessage(ValidationMessages.InvalidFormat("Postal code", "alphanumeric characters"));

    // ── String tier helpers ───────────────────────────────────────────────

    public static IRuleBuilderOptions<T, string> MustBeInlineStandard<T>(
        this IRuleBuilder<T, string> rule, string fieldName, bool required = true)
    {
        var builder = required
            ? rule.NotEmpty().WithMessage(ValidationMessages.Required(fieldName))
            : rule.NotNull().WithMessage(ValidationMessages.Required(fieldName));
        return builder
            .MaximumLength(FieldLimits.InlineStandard.MaxLength)
            .WithMessage(ValidationMessages.MaxLength(fieldName, FieldLimits.InlineStandard.MaxLength));
    }

    public static IRuleBuilderOptions<T, string> MustBeInlineShort<T>(
        this IRuleBuilder<T, string> rule, string fieldName, bool required = true)
    {
        var builder = required
            ? rule.NotEmpty().WithMessage(ValidationMessages.Required(fieldName))
            : rule.NotNull().WithMessage(ValidationMessages.Required(fieldName));
        return builder
            .MaximumLength(FieldLimits.InlineShort.MaxLength)
            .WithMessage(ValidationMessages.MaxLength(fieldName, FieldLimits.InlineShort.MaxLength));
    }

    public static IRuleBuilderOptions<T, string> MustBeInlineExtended<T>(
        this IRuleBuilder<T, string> rule, string fieldName, bool required = false)
    {
        var builder = required
            ? rule.NotEmpty().WithMessage(ValidationMessages.Required(fieldName))
            : rule.NotNull();
        return builder
            .MaximumLength(FieldLimits.InlineExtended.MaxLength)
            .WithMessage(ValidationMessages.MaxLength(fieldName, FieldLimits.InlineExtended.MaxLength));
    }

    public static IRuleBuilderOptions<T, string> MustBeMultilineStandard<T>(
        this IRuleBuilder<T, string> rule, string fieldName, bool required = false)
    {
        var builder = required
            ? rule.NotEmpty().WithMessage(ValidationMessages.Required(fieldName))
            : rule.NotNull();
        return builder
            .MaximumLength(FieldLimits.MultilineStandard.MaxLength)
            .WithMessage(ValidationMessages.MaxLength(fieldName, FieldLimits.MultilineStandard.MaxLength));
    }

    public static IRuleBuilderOptions<T, string> MustBeMultilineLong<T>(
        this IRuleBuilder<T, string> rule, string fieldName, bool required = false)
    {
        var builder = required
            ? rule.NotEmpty().WithMessage(ValidationMessages.Required(fieldName))
            : rule.NotNull();
        return builder
            .MaximumLength(FieldLimits.MultilineLong.MaxLength)
            .WithMessage(ValidationMessages.MaxLength(fieldName, FieldLimits.MultilineLong.MaxLength));
    }

    public static IRuleBuilderOptions<T, string> MustBeMultilineDocument<T>(
        this IRuleBuilder<T, string> rule, string fieldName, bool required = true)
    {
        var builder = required
            ? rule.NotEmpty().WithMessage(ValidationMessages.Required(fieldName))
            : rule.NotNull();
        return builder
            .MaximumLength(FieldLimits.MultilineDocument.MaxLength)
            .WithMessage(ValidationMessages.MaxLength(fieldName, FieldLimits.MultilineDocument.MaxLength));
    }

    // ── Pagination ────────────────────────────────────────────────────────

    public static void MustBeValidPagination<T>(
        this AbstractValidator<T> validator,
        Expression<Func<T, int>> pageNumberExpr,
        Expression<Func<T, int>> pageSizeExpr)
    {
        validator.RuleFor(pageNumberExpr)
            .GreaterThanOrEqualTo(FieldLimits.Pagination.PageNumberMin)
            .WithMessage(ValidationMessages.MinValue("Page number", FieldLimits.Pagination.PageNumberMin));
        validator.RuleFor(pageSizeExpr)
            .InclusiveBetween(FieldLimits.Pagination.PageSizeMin, FieldLimits.Pagination.PageSizeMax)
            .WithMessage(ValidationMessages.RangeBetween("Page size",
                FieldLimits.Pagination.PageSizeMin, FieldLimits.Pagination.PageSizeMax));
    }

    // ── Rating ────────────────────────────────────────────────────────────

    public static IRuleBuilderOptions<T, int> MustBeValidRating<T>(
        this IRuleBuilder<T, int> rule, string fieldName = "Rating")
        => rule
            .InclusiveBetween(FieldLimits.Rating.Min, FieldLimits.Rating.Max)
            .WithMessage(ValidationMessages.RangeBetween(fieldName,
                FieldLimits.Rating.Min, FieldLimits.Rating.Max));

    // ── Concurrency Token ─────────────────────────────────────────────────

    /// <summary>
    ///     Validates that an UpdateToken (optimistic concurrency) is not empty.
    ///     Commands that update existing entities must include a valid UpdateToken.
    /// </summary>
    public static IRuleBuilderOptions<T, Guid> MustBeValidUpdateToken<T>(
        this IRuleBuilder<T, Guid> rule, string fieldName = "UpdateToken")
        => rule
            .NotEmpty().WithMessage(ValidationMessages.Required(fieldName));

    /// <summary>
    ///     Validates UpdateToken within a collection item (bulk operations).
    /// </summary>
    public static IRuleBuilderOptions<T, Guid> MustBeValidUpdateToken<T, TElement>(
        this IRuleBuilder<T, Guid> rule, string fieldName = "UpdateToken")
        => rule
            .NotEmpty().WithMessage(ValidationMessages.Required(fieldName));
}
