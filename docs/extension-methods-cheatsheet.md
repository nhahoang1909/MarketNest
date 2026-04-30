# Extension Methods — Quick Reference Card

> Bookmark this! Common patterns used across MarketNest codebase.

---

## String Validation (inline checks)

```csharp
email.IsValidEmail()              // RFC 5321 email validation
phone.IsValidPhoneNumber()        // E.164 format (+country + number)
slug.IsValidSlug()                // lowercase-alphanumeric-hyphens
url.IsValidUrl()                  // http(s):// URLs only
countryCode.IsValidCountryCode()  // ISO 3166-1 alpha-2 (e.g., "VN")
currencyCode.IsValidCurrencyCode() // ISO 4217 (e.g., "USD")
postalCode.IsValidPostalCode()    // 3-20 alphanumeric + space/hyphen

// Password strength
password.IsStrongPassword()       // 8+ chars, upper, lower, digit, special
password.ContainsDigit()
password.ContainsUpperCase()
password.ContainsLowerCase()
password.ContainsSpecialChar()
```

---

## String Transformations

```csharp
text.Truncate(100)                // "Long text…" (ellipsis at 100 chars)
title.ToSlug()                    // "product-name" (URL-friendly)
"Nguyễn".RemoveDiacritics()       // "Nguyen" (strip accents)
email.MaskEmail()                 // "jo***@example.com"
phone.MaskPhone()                 // "****5678"
"PascalCase".ToTitleCase()        // "Pascal Case"
"ProductName".ToSnakeCase()       // "product_name"
html.StripHtml()                  // Remove all <tags>
longText.FirstWords(10)           // "First ten words…"
```

---

## String Null Handling

```csharp
text.IsNullOrEmpty()              // bool
text.IsNullOrWhiteSpace()         // bool
text.HasValue()                   // !IsNullOrWhiteSpace
text.NullIfEmpty()                // Convert "" → null
text.NullIfWhiteSpace()           // Convert "  " → null
text.OrDefault("Fallback")        // "Fallback" if null/empty/whitespace
```

---

## DateTime Formatting

```csharp
// With time zone
order.CreatedAt.FormatAsDateOnly(tz)       // "2026-04-30"
order.CreatedAt.FormatAsDateTime(tz)       // "2026-04-30 14:30"
order.CreatedAt.FormatAsRelative()         // "5m ago"

// Nullable overloads
order.CompletedAt.FormatAsDateOnly(tz, "—")  // "—" if null
order.CompletedAt.FormatAsRelative("never")   // "never" if null

// Checks
date.IsInFuture()                 // vs UTC now
date.IsInPast()
date.IsBetween(start, end)
date.IsToday(userTimeZone)

// Boundaries
date.StartOfDay()                 // 00:00:00
date.EndOfDay()                   // 23:59:59.999...
```

---

## Enum Extensions

```csharp
// With [Description] attributes
status.ToDescription()            // "Pending Payment"
EnumExtensions.FromDescription<OrderStatus>("Shipped")  // OrderStatus.Shipped

// Safe parsing
EnumExtensions.ParseOrNull<OrderStatus>("invalid")  // null
EnumExtensions.ParseOrDefault("invalid", defaultValue)

// Utility
EnumExtensions.GetValues<OrderStatus>()               // all values
EnumExtensions.GetValuesWithDescriptions<OrderStatus>()  // [(value, "label"), ...]
status.IsDefined()                // bool
```

---

## Numeric Extensions

```csharp
value.Clamp(0, 100)               // Math.Clamp
value.IsBetween(min, max)
value.IsPositive()                // > 0
value.IsNonNegative()             // >= 0

1500.ToCompactString()            // "1.5K"
1_000_000L.ToCompactString()      // "1M"
1234567.ToFormattedNumber()       // "1,234,567"
0.156m.ToPercentageString()       // "15.6%"
22.ToOrdinal()                    // "22nd"
1048576L.ToFileSize()             // "1 MB"
```

---

## Collection Extensions

```csharp
list.IsNullOrEmpty()              // null-safe check
list.OrEmpty()                    // Convert null → empty IEnumerable
items.Batch(100)                  // Split into fixed-size batches
items.ForEach(item => ...)
items.ForEach((item, index) => ...)
list.ElementAtOrDefault(5, "N/A") // Safe indexing
tags.JoinNonEmpty(", ")           // Join non-null/empty strings
```

---

## Common Patterns

### Auto-generate slug

```csharp
command.Slug = command.Name.ToSlug();  // "Áo Dài" → "ao-dai"
```

### Display relative time

```csharp
@order.CreatedAt.FormatAsRelative()   // "5m ago"
```

### Enum dropdown

```cshtml
@foreach (var (value, label) in EnumExtensions.GetValuesWithDescriptions<OrderStatus>())
{
    <option value="@value">@label</option>
}
```

### Guard clauses

```csharp
if (!email.IsValidEmail())
    return Error.Validation("INVALID_EMAIL", "Email format is invalid");
```

### Batch processing

```csharp
foreach (var batch in productIds.Batch(100))
{
    await UpdateBatchAsync(batch);
}
```

---

**Full documentation**: `docs/common-extension-methods.md`

