# MarketNest — Common Validation Rules

> Version: 1.0 | Date: 2026-04-30
> Location: `src/Base/MarketNest.Base.Common/Validation/`

---

## Overview

This document describes the centralized validation infrastructure used across the entire MarketNest project. All validators, EF Core configurations, and Razor views must reference these shared constants and messages.

### Key Files

| File | Purpose |
|------|---------|
| `FieldLimits.cs` | Single source of truth for all numeric limits (lengths, ranges, sizes) |
| `ValidationMessages.cs` | Human-readable error message factory — no inline strings in validators |
| `ValidatorExtensions.cs` | Reusable FluentValidation extension methods for common patterns |

All files live in `src/Base/MarketNest.Base.Common/Validation/` with namespace `MarketNest.Base.Common`.

---

## 1. Principles

1. **Every string field** (except notification template body) must have a `MaximumLength` — no unbounded strings in the database.
2. **Every rule must use `ValidationMessages`** — no inline string messages in validators.
3. **Validators only validate format/length/required** — business rules belong in the Domain layer.
4. **Excel import uses the same rules** — no "lenient mode" for import.
5. **FieldLimits is the single source of truth** — never hardcode numbers elsewhere.

---

## 2. String Field Tiers

| Tier | Max Length | Use Case | Examples |
|------|-----------|----------|----------|
| **Identifier** | 50 | Code, slug, SKU, short key | `Slug`, `Sku`, `CouponCode` |
| **Inline Short** | 100 | Short names, labels | `RecipientName`, `City`, `ReviewTitle` |
| **Inline Standard** | 255 | Entity names | `ProductName`, `StorefrontName` |
| **Inline Extended** | 500 | Bio, tagline | `PublicBio`, `Tagline`, `SellerReply` |
| **Multiline Standard** | 2000 | Notes, comments | `AdminNote`, `DisputeMessage` |
| **Multiline Long** | 5000 | Short descriptions | `ShortDescription` |
| **Multiline Document** | 20000 | Full rich-text | `ProductDescription` |
| **Unbounded** | *(none)* | Admin-only template body | `NotificationTemplateBody` |

### Domain-specific classes reference tiers

```csharp
FieldLimits.Product.NameMaxLength        == FieldLimits.InlineStandard.MaxLength  // 255
FieldLimits.Storefront.TaglineMaxLength  == FieldLimits.InlineExtended.MaxLength  // 500
FieldLimits.Review.BodyMaxLength         == FieldLimits.InlineExtended.MaxLength  // 500
```

---

## 3. Numeric Limits

| Category | Constant | Value |
|----------|----------|-------|
| **Money** | Min | 0.01 |
| | Max | 999,999.99 |
| | Decimal places | 2 |
| **Percentage** | Range | 0–100 |
| | Decimal places | 4 |
| **Cart quantity** | Range | 1–99 |
| **Stock quantity** | Range | 0–999,999 |
| **Rating** | Range | 1–5 |
| **Weight** | Range | 0.001–999.999 (3dp) |
| **Pagination** | Page min | 1 |
| | Page size | 1–100 |

---

## 4. Special Format Fields

| Format | Validation | Constant |
|--------|-----------|----------|
| Email | RFC 5322 + max 254 | `FieldLimits.Email.MaxLength` |
| Phone | E.164 pattern | `FieldLimits.PhoneNumber.Pattern` |
| URL | Absolute HTTP(S) + max 500 | `FieldLimits.Url.MaxLength` |
| Slug | `^[a-z0-9-]{3,50}$` | `FieldLimits.Slug.Pattern` |
| Country code | 2-char ISO 3166-1 alpha-2 | `FieldLimits.CountryCode` |
| Currency code | 3-char ISO 4217 | `FieldLimits.CurrencyCode` |
| Postal code | `^[A-Z0-9\s-]{3,20}$` | `FieldLimits.PostalCode.Pattern` |
| Timezone | IANA timezone ID | Validated via `TimeZoneInfo` |

---

## 5. File Upload Limits

| Type | Max Size | Allowed MIME Types |
|------|----------|-------------------|
| Image | 5 MB | `image/jpeg`, `image/png`, `image/webp` |
| Excel | 10 MB | `.xlsx`, `.xls` MIME types |

---

## 6. Collection Limits

| Collection | Min | Max |
|-----------|-----|-----|
| Product images | 1 | 10 |
| Variants per product | 1 | 50 |
| Tags per product | 0 | 20 |
| Cart items | 1 | 50 |
| Wishlist items | 0 | 200 |

---

## 7. ValidatorExtensions — Available Methods

### String Tier Helpers
- `MustBeSlug(fieldName)` — required + slug pattern + max 50
- `MustBeInlineShort(fieldName, required)` — max 100
- `MustBeInlineStandard(fieldName, required)` — max 255
- `MustBeInlineExtended(fieldName, required)` — max 500
- `MustBeMultilineStandard(fieldName, required)` — max 2000
- `MustBeMultilineLong(fieldName, required)` — max 5000
- `MustBeMultilineDocument(fieldName, required)` — max 20000

### Format Validators
- `MustBeValidEmail(fieldName)` — required + email format + max 254
- `MustBeValidPhone()` — E.164 regex (optional field pattern)
- `MustBeValidUrl(fieldName)` — absolute HTTP(S) + max 500
- `MustBeValidPostalCode()` — required + pattern + max 20
- `MustBeValidCountryCode()` — required + 2 uppercase letters
- `MustBeValidCurrencyCode()` — required + 3 uppercase letters
- `MustBeValidTimezone()` — required + system timezone lookup
- `MustBeValidId(fieldName)` — not Guid.Empty

### Numeric Validators
- `MustBePositiveMoney(fieldName)` — > 0, ≤ 999999.99, 2dp
- `MustBeNonNegativeMoney(fieldName)` — ≥ 0, ≤ 999999.99, 2dp
- `MustBeValidQuantity(fieldName)` — 1–99
- `MustBeValidStockQuantity(fieldName)` — 0–999999
- `MustBeValidPercentage(fieldName)` — 0–100, 4dp
- `MustBeValidRating(fieldName)` — 1–5

### Composite
- `MustBeValidPagination(validator, pageExpr, pageSizeExpr)` — page ≥ 1, pageSize 1–100

---

## 8. Usage Examples

### FluentValidation Validator

```csharp
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .MustBeInlineStandard("Product name");

        RuleFor(x => x.Slug)
            .MustBeSlug("Product slug");

        RuleFor(x => x.Price)
            .MustBePositiveMoney("Price");

        RuleFor(x => x.StockQuantity)
            .MustBeValidStockQuantity();

        RuleFor(x => x.Description)
            .MustBeMultilineDocument("Description", required: false);
    }
}
```

### EF Core Configuration

```csharp
builder.Property(p => p.Name)
    .HasMaxLength(FieldLimits.Product.NameMaxLength)
    .IsRequired();

builder.Property(p => p.Slug)
    .HasMaxLength(FieldLimits.Slug.MaxLength)
    .IsRequired();
```

### Razor View

```html
<input asp-for="ProductName"
       maxlength="@FieldLimits.Product.NameMaxLength"
       class="..." />
```

---

## 9. ValidationMessages — Categories

| Category | Methods |
|----------|---------|
| Required | `Required(field)` |
| Length | `MaxLength`, `MinLength`, `ExactLength`, `LengthBetween` |
| Numeric | `MustBePositive`, `MinValue`, `MaxValue`, `RangeBetween`, `MaxDecimalPlaces` |
| Format | `InvalidFormat`, `InvalidSlugFormat`, `InvalidPhoneFormat`, `InvalidEmailFormat` |
| Date | `DateMustBeBefore`, `DateMustBeAfter`, `DateMustBeInFuture`, `DateMustNotBePast` |
| File | `InvalidFileType`, `FileTooLarge` |
| Collection | `CollectionMinItems`, `CollectionMaxItems` |
| Identity | `InvalidId`, `NotFound`, `AlreadyExists` |
| Excel | `ExcelColumnRequired`, `ExcelColumnMaxLength`, `ExcelColumnInvalidFormat`, `ExcelColumnRangeBetween`, `ExcelColumnNotFound`, `ExcelSheetNotFound`, `ExcelFileEmpty`, `ExcelTooManyRows` |

---

## 10. Excel Import Rules

- Same validation rules as API — no lenient mode
- Max **1000 rows** per batch (`FieldLimits.ExcelImport.MaxRowsPerBatch`)
- Max **100 errors** reported (`FieldLimits.ExcelImport.MaxErrorsReported`)
- Row-level error messages include row number
- Header row must match column names (case-insensitive)

---

## 11. Adding New Fields — Checklist

1. Choose appropriate tier from §2 or define in a domain-specific class
2. Add constant to `FieldLimits.cs` (reference tier, don't duplicate numbers)
3. Use `ValidationMessages` for error text
4. Use `ValidatorExtensions` method if pattern matches
5. Update EF Core configuration `HasMaxLength()`
6. Add `maxlength` attribute in Razor view
7. Update Field Reference Table below

---

## 12. Field Reference Table

| Module | Field | Type | Required | Rule |
|--------|-------|------|----------|------|
| **Identity** | `Email` | string | ✅ | MaxLength(254), Email format |
| | `DisplayName` | string | ✅ | MaxLength(255) |
| | `PhoneNumber` | string? | ❌ | E.164 format |
| | `PublicBio` | string? | ❌ | MaxLength(500) |
| | `RecipientName` | string | ✅ | MaxLength(100) |
| | `Street` | string | ✅ | MaxLength(200) |
| | `City` | string | ✅ | MaxLength(100) |
| | `PostalCode` | string | ✅ | MaxLength(20) |
| | `CountryCode` | string | ✅ | ISO 3166-1 alpha-2 |
| **Catalog** | `StorefrontName` | string | ✅ | MaxLength(255) |
| | `StorefrontSlug` | string | ✅ | MaxLength(50), slug |
| | `StorefrontTagline` | string? | ❌ | MaxLength(500) |
| | `StorefrontDescription` | string? | ❌ | MaxLength(20000) |
| | `ProductName` | string | ✅ | MaxLength(255) |
| | `ProductSlug` | string | ✅ | MaxLength(50), slug |
| | `ProductShortDescription` | string? | ❌ | MaxLength(5000) |
| | `ProductDescription` | string? | ❌ | MaxLength(20000) |
| | `Sku` | string | ✅ | MaxLength(50) |
| | `BasePrice` | decimal | ✅ | > 0, ≤ 999999.99, 2dp |
| | `SalePrice` | decimal? | ❌ | ≥ 0, ≤ 999999.99, 2dp |
| | `StockQuantity` | int | ✅ | 0–999999 |
| **Cart** | `CartItem.Quantity` | int | ✅ | 1–99 |
| **Orders** | `OrderNote` | string? | ❌ | MaxLength(2000) |
| **Reviews** | `ReviewTitle` | string? | ❌ | MaxLength(100) |
| | `ReviewBody` | string? | ❌ | MaxLength(500) |
| | `SellerReplyBody` | string | ✅ | MaxLength(500) |
| **Disputes** | `DisputeMessageBody` | string | ✅ | MaxLength(2000) |
| **Payments** | `CommissionRate` | decimal | ✅ | 0–100, 4dp |
| **Admin** | `SuspensionReason` | string | ✅ | MaxLength(2000) |
| | `AdminNote` | string? | ❌ | MaxLength(2000) |
| **Notifications** | `TemplateSubject` | string | ✅ | MaxLength(255) |
| | `TemplateBody` | string | ✅ | Unbounded (admin-only) |

