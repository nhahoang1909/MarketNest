# MarketNest — Common Extension Methods

> **Location**: `src/Base/MarketNest.Base.Common/`  
> **Namespace**: `MarketNest.Base.Common`  
> **Date**: 2026-04-30

All extension methods are globally available across the entire codebase via `GlobalUsings.cs` in each project.

---

## Table of Contents

1. [DateTimeOffset Extensions](#datetimeoffset-extensions)
2. [Enum Extensions](#enum-extensions)
3. [String Extensions](#string-extensions)
4. [Numeric Extensions](#numeric-extensions)
5. [Collection Extensions](#collection-extensions)

---

## DateTimeOffset Extensions

**File**: `DateTimeOffsetExtensions.cs`

### Comparison & Predicates

```csharp
DateTimeOffset now = DateTimeOffset.UtcNow;
DateTimeOffset future = now.AddDays(5);

future.IsInFuture()    // true
now.IsInPast()         // false
now.IsBetween(start, end)  // true/false
now.IsToday(userTimeZone)  // true if today in user's time zone
```

### Day Boundaries

```csharp
DateTimeOffset now = DateTimeOffset.UtcNow;

DateTimeOffset startOfDay = now.StartOfDay();  // 00:00:00
DateTimeOffset endOfDay = now.EndOfDay();      // 23:59:59.9999999
```

### Formatting (with time zone)

```csharp
TimeZoneInfo vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

now.FormatAsDateOnly(vnTimeZone)         // "2026-04-30"
now.FormatAsDateTime(vnTimeZone)         // "2026-04-30 14:30"
now.FormatAsDateTimeFull(vnTimeZone)     // "2026-04-30 14:30:05"
now.FormatAsMonthDayYear(vnTimeZone)     // "Apr 30, 2026"
now.FormatAsRelative()                   // "5m ago", "2h ago", "1d ago"
```

### Nullable Overloads

```csharp
DateTimeOffset? nullableDate = order.CompletedAt;

nullableDate.FormatAsDateOnly(vnTimeZone, "—")      // "—" if null
nullableDate.FormatAsDateTime(vnTimeZone, "N/A")    // Custom fallback
nullableDate.FormatAsRelative("never")              // "never" if null
```

---

## Enum Extensions

**File**: `EnumExtensions.cs`

### Using [Description] Attributes

```csharp
using System.ComponentModel;

public enum OrderStatus
{
    [Description("Pending Payment")]
    PendingPayment,
    
    [Description("Confirmed")]
    Confirmed,
    
    [Description("Shipped")]
    Shipped
}

// Convert enum to display text
OrderStatus status = OrderStatus.PendingPayment;
string displayText = status.ToDescription();  // "Pending Payment"

// Parse from description
OrderStatus? parsed = EnumExtensions.FromDescription<OrderStatus>("Shipped");  // OrderStatus.Shipped
OrderStatus required = EnumExtensions.FromDescriptionRequired<OrderStatus>("Confirmed");  // throws if not found
```

### Safe Parsing

```csharp
// Case-insensitive parse, returns null if invalid
OrderStatus? status = EnumExtensions.ParseOrNull<OrderStatus>("confirmed");  // OrderStatus.Confirmed
OrderStatus? invalid = EnumExtensions.ParseOrNull<OrderStatus>("invalid");   // null

// Parse with default fallback
OrderStatus status = EnumExtensions.ParseOrDefault("invalid", OrderStatus.PendingPayment);
```

### Utility Methods

```csharp
// Get all enum values
IReadOnlyList<OrderStatus> allStatuses = EnumExtensions.GetValues<OrderStatus>();

// Get all values with descriptions (useful for dropdowns)
var valuesWithLabels = EnumExtensions.GetValuesWithDescriptions<OrderStatus>();
// [(PendingPayment, "Pending Payment"), (Confirmed, "Confirmed"), ...]

// Check if defined
OrderStatus status = OrderStatus.Shipped;
bool valid = status.IsDefined();  // true

bool validInt = EnumExtensions.IsDefined<OrderStatus>(99);  // false
```

---

## String Extensions

**File**: `StringExtensions.cs`

### Null/Empty Checks

```csharp
string? value = "  ";

value.IsNullOrEmpty()       // false
value.IsNullOrWhiteSpace()  // true
value.HasValue()            // false

string? empty = null;
empty.IsNullOrEmpty()       // true
```

### Null Coalescing

```csharp
string? value = null;
string result = value.NullIfEmpty();       // null
string result = value.NullIfWhiteSpace();  // null
string result = value.OrDefault("N/A");    // "N/A"

string? whitespace = "   ";
whitespace.NullIfWhiteSpace();  // null
```

### Truncation

```csharp
string longText = "Lorem ipsum dolor sit amet";

longText.Truncate(10)           // "Lorem ips…"
longText.Truncate(10, "...")    // "Lorem i..."
longText.Truncate(100)          // "Lorem ipsum dolor sit amet" (no truncation)
```

### Slug Generation

```csharp
string title = "Áo Dài Truyền Thống";
string slug = title.ToSlug();  // "ao-dai-truyen-thong"

"Hello World!".ToSlug()        // "hello-world"
"Product___Name".ToSlug()      // "product-name"
"Café Sữa Đá".ToSlug()         // "cafe-sua-da"
```

### Diacritics

```csharp
string vietnamese = "Nguyễn Văn A";
string normalized = vietnamese.RemoveDiacritics();  // "Nguyen Van A"

"café".RemoveDiacritics()      // "cafe"
"Müller".RemoveDiacritics()    // "Muller"
```

### Masking (for display)

```csharp
string email = "john.doe@example.com";
email.MaskEmail()              // "jo***@example.com"

string phone = "+84912345678";
phone.MaskPhone()              // "****5678"
```

### Casing Transforms

```csharp
"productName".ToTitleCase()    // "Product Name"
"ProductName".ToTitleCase()    // "Product Name"

"ProductName".ToSnakeCase()    // "product_name"
"Product Name".ToSnakeCase()   // "product_name"
```

### Content Utilities

```csharp
string longText = "Lorem ipsum dolor sit amet consectetur adipiscing elit";
longText.FirstWords(3)         // "Lorem ipsum dolor…"
longText.FirstWords(3, "...")  // "Lorem ipsum dolor..."

string html = "<p>Hello <strong>World</strong></p>";
html.StripHtml()               // "Hello World"
```

### Validation Checks

All validation methods return `bool` — use these for quick inline checks. For FluentValidation rules, use `ValidatorExtensions` instead.

```csharp
// Email
"user@example.com".IsValidEmail()     // true
"invalid".IsValidEmail()              // false

// Phone (E.164 format: +[country][number])
"+84912345678".IsValidPhoneNumber()   // true
"0912345678".IsValidPhoneNumber()     // false (missing +)

// Slug
"my-product-123".IsValidSlug()        // true
"My Product".IsValidSlug()            // false (uppercase, spaces)

// URL
"https://example.com".IsValidUrl()    // true
"example.com".IsValidUrl()            // false (no scheme)

// Country Code (ISO 3166-1 alpha-2)
"VN".IsValidCountryCode()             // true
"Vietnam".IsValidCountryCode()        // false

// Currency Code (ISO 4217)
"USD".IsValidCurrencyCode()           // true
"US".IsValidCurrencyCode()            // false

// Postal Code
"10000".IsValidPostalCode()           // true
"AB-123".IsValidPostalCode()          // true
"x".IsValidPostalCode()               // false (too short)
```

### Character Content Checks (for password validation)

```csharp
string value = "HelloWorld123!";

value.IsNumeric()              // false (has letters)
value.IsAlphanumeric()         // false (has !)
value.ContainsDigit()          // true
value.ContainsUpperCase()      // true
value.ContainsLowerCase()      // true
value.ContainsSpecialChar()    // true

// Combined strength check
value.IsStrongPassword()       // true (8+ chars, upper, lower, digit, special)

"password".IsStrongPassword()  // false (no upper, digit, special)
```

---

## Numeric Extensions

**File**: `NumericExtensions.cs`

### Clamping

```csharp
int value = 150;
value.Clamp(0, 100)            // 100

decimal price = -5.0m;
price.Clamp(0m, 999.99m)       // 0
```

### Range Checks

```csharp
int quantity = 50;
quantity.IsBetween(1, 100)     // true
quantity.IsPositive()          // true
quantity.IsNonNegative()       // true

decimal amount = -5.0m;
amount.IsPositive()            // false
amount.IsNonNegative()         // false
```

### Formatting

```csharp
// Compact notation
1500.ToCompactString()         // "1.5K"
1_500_000.ToCompactString()    // "1.5M"
2_300_000_000L.ToCompactString()  // "2.3B"

// Formatted numbers with thousand separators
1234567.ToFormattedNumber()    // "1,234,567"
1234.56m.ToFormattedNumber(2)  // "1,234.57"

// Percentage
0.156m.ToPercentageString()    // "15.6%"
85m.ToPercentageString()       // "85.0%"

// Ordinal
1.ToOrdinal()                  // "1st"
2.ToOrdinal()                  // "2nd"
3.ToOrdinal()                  // "3rd"
22.ToOrdinal()                 // "22nd"
```

### File Size

```csharp
1536L.ToFileSize()             // "1.5 KB"
1048576L.ToFileSize()          // "1 MB"
(5 * 1024 * 1024).ToFileSize() // "5 MB"
```

---

## Collection Extensions

**File**: `CollectionExtensions.cs`

### Null-Safe Checks

```csharp
List<string>? items = null;
items.IsNullOrEmpty()          // true

items = new List<string>();
items.IsNullOrEmpty()          // true

items.Add("test");
items.IsNullOrEmpty()          // false

// Return empty if null (safe enumeration)
IEnumerable<string> safe = items.OrEmpty();
```

### Batching

```csharp
var numbers = Enumerable.Range(1, 100);
var batches = numbers.Batch(10);

foreach (var batch in batches)
{
    Console.WriteLine($"Processing {batch.Count} items");
    // batch is IReadOnlyList<int> with max 10 items
}
```

### ForEach

```csharp
var items = new[] { "a", "b", "c" };

items.ForEach(item => Console.WriteLine(item));

items.ForEach((item, index) => 
    Console.WriteLine($"{index}: {item}"));
// Output:
// 0: a
// 1: b
// 2: c
```

### Safe Element Access

```csharp
var list = new List<string> { "a", "b", "c" };

string? value = list.ElementAtOrDefault(5);        // null (out of range)
string value = list.ElementAtOrDefault(5, "N/A");  // "N/A"
```

### Safe Dictionary Access (MN036)

Direct dictionary indexer access (`dict[key]`) throws `KeyNotFoundException` when the key is absent,
crashing the request. Use one of these safe alternatives instead.

**`GetValueOrDefault`** — returns a fallback instead of throwing:

```csharp
// Works on IDictionary<TKey,TValue> and IReadOnlyDictionary<TKey,TValue>
decimal fee = configMap.GetValueOrDefault("shipping_fee", defaultValue: 0m);
string label = readonlyMap.GetValueOrDefault("status_label", "—");

// ❌ DO NOT — throws KeyNotFoundException when key is missing
decimal fee = configMap["shipping_fee"];
```

**`TryGet`** — returns a `(bool Found, TValue? Value)` tuple for pattern-matching:

```csharp
var (found, price) = pricingMap.TryGet("SKU-001");
if (found) ApplyDiscount(price!);

// Or with deconstruction in a condition
if (configMap.TryGet("feature_flag") is (true, var flag))
    EnableFeature(flag!);
```

**When to use which:**

| Pattern | Use when |
|---------|----------|
| `TryGetValue(key, out var v)` | You need the value only if it exists (classic pattern) |
| `GetValueOrDefault(key, fallback)` | You always need a value and have a sensible default |
| `TryGet(key)` | You need both the existence flag and value in a single expression |

> The analyzer **MN036** warns on all direct dictionary indexer uses. Suppress with
> `#pragma warning disable MN036` only when the key is guaranteed to exist (e.g. after
> a `ContainsKey` guard), with a comment explaining why.

### String Joining (non-empty only)

```csharp
var tags = new[] { "tag1", "", null, "tag2", "   ", "tag3" };
string joined = tags.JoinNonEmpty(", ");  // "tag1, tag2, tag3"

var parts = new[] { "Street", null, "City" };
string address = parts.JoinNonEmpty(" | ");  // "Street | City"
```

---

## Usage Patterns

### Example 1: Order Display

```csharp
public class OrderDto
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public OrderStatus Status { get; set; }
}

// In a query handler or view model
var userTimeZone = TimeZoneInfo.FindSystemTimeZoneById(user.TimeZone);

var viewModel = new OrderViewModel
{
    CreatedAt = order.CreatedAt.FormatAsDateTimeFull(userTimeZone),
    CreatedTimeAgo = order.CreatedAt.FormatAsRelative(),
    CompletedAt = order.CompletedAt.FormatAsDateTime(userTimeZone, "Not completed"),
    StatusLabel = order.Status.ToDescription(),
    StatusBadge = GetStatusBadge(order.Status)
};
```

### Example 2: User Input Validation

```csharp
public class CreateUserCommand
{
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// Quick pre-validation checks (before FluentValidation)
if (!command.Email.IsValidEmail())
    return Error.Validation("INVALID_EMAIL", "Email format is invalid");

if (!command.PhoneNumber.IsValidPhoneNumber())
    return Error.Validation("INVALID_PHONE", "Phone must be in E.164 format (+84...)");

if (!command.Password.IsStrongPassword())
    return Error.Validation("WEAK_PASSWORD", "Password must be at least 8 characters with upper, lower, digit, and special character");
```

### Example 3: Product Slug Generation

```csharp
public class CreateProductCommand
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

// Auto-generate slug from name if not provided
if (command.Slug.IsNullOrWhiteSpace())
{
    command.Slug = command.Name.ToSlug();
}

// Validate slug format
if (!command.Slug.IsValidSlug())
    return Error.Validation("INVALID_SLUG", "Slug must be lowercase alphanumeric with hyphens only");
```

### Example 4: Enum Dropdowns in Razor

```cshtml
@using MarketNest.Base.Common

<select name="status" class="form-select">
    @foreach (var (value, description) in EnumExtensions.GetValuesWithDescriptions<OrderStatus>())
    {
        <option value="@value">@description</option>
    }
</select>

@* Output:
<option value="PendingPayment">Pending Payment</option>
<option value="Confirmed">Confirmed</option>
<option value="Shipped">Shipped</option>
*@
```

### Example 5: Safe Collection Operations

```csharp
// Process large dataset in batches
var productIds = await GetAllProductIdsAsync();

foreach (var batch in productIds.Batch(100))
{
    await UpdatePricesInBatchAsync(batch);
}

// Safe iteration over nullable collection
IEnumerable<string>? tagList = product.Tags;
foreach (var tag in tagList.OrEmpty())
{
    Console.WriteLine(tag);  // Never throws NullReferenceException
}

// Display tags with non-empty join
var displayTags = product.Tags?.JoinNonEmpty(", ") ?? "No tags";
```

---

## Integration with Existing Infrastructure

### Works with FieldLimits

```csharp
using MarketNest.Base.Common;

// String validation aligns with FieldLimits constants
string email = "user@example.com";
if (email.IsValidEmail() && email.Length <= FieldLimits.Email.MaxLength)
{
    // Email is valid
}

string slug = command.Name.ToSlug();
if (slug.IsValidSlug())  // Checks FieldLimits.Slug.Pattern internally
{
    // Slug is valid
}
```

### Complements ValidatorExtensions

```csharp
using FluentValidation;
using MarketNest.Base.Common;

// For validators: use ValidatorExtensions
public class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Email).MustBeValidEmail();  // FluentValidation rule
    }
}

// For inline checks: use string extensions
public async Task<Result<Unit, Error>> HandleAsync(CreateUserCommand cmd)
{
    // Quick guard clause
    if (!cmd.Email.IsValidEmail())
        return Error.Validation("INVALID_EMAIL", "Invalid email format");
    
    // ... rest of handler
}
```

---

## Performance Notes

1. **Enum caching**: `ToDescription()` caches reflection results in a `ConcurrentDictionary` for fast repeated lookups.
2. **Regex source generators**: All regex patterns use `[GeneratedRegex]` for compile-time optimization.
3. **Null checks first**: All extensions check for null/empty before performing operations.
4. **No allocations on simple checks**: Methods like `IsPositive()`, `IsBetween()`, `HasValue()` are allocation-free.

---

## Adding New Extensions

When adding new extension methods:

1. **Choose the correct file**:
   - Date/time → `DateTimeOffsetExtensions.cs`
   - Enum → `EnumExtensions.cs`
   - String → `StringExtensions.cs`
   - Numeric → `NumericExtensions.cs`
   - Collections → `CollectionExtensions.cs`

2. **Follow naming conventions**:
   - Boolean checks: `Is*`, `Has*`, `Contains*`
   - Transformations: `To*`, `As*`
   - Coalescing: `OrDefault`, `NullIf*`

3. **Always null-safe**: Check `string.IsNullOrWhiteSpace()` / `source is null` first.

4. **Use existing constants**: Reference `FieldLimits.*`, `DomainConstants.*` instead of magic numbers.

5. **Document with XML comments and examples**.

6. **Add unit tests** in `tests/MarketNest.UnitTests/Base/`.

---

## Related Documentation

- `docs/common-validation-rules.md` — FieldLimits, ValidationMessages, ValidatorExtensions
- `docs/code-rules.md` — Project coding conventions
- `src/Base/MarketNest.Base.Common/Validation/` — Validation infrastructure

---

**End of document**

