# Nullable Management — Code Rules

> **Applies to**: Entire MarketNest codebase
> **Enforcement**: `<Nullable>enable</Nullable>` + `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` in `Directory.Build.props`
> **Core principle**: Compiler is the first line of defense — if a nullable warning exists, CI fails.

---

## Core Philosophy

**Nullable is not an implementation detail — it is a business decision.**

`string?` does not mean "I'm not sure". It means **"this field is allowed to be absent per domain logic"**. Every `?` must have a clear domain reason, not just to silence a compiler warning.

```
Non-nullable  →  "This field MUST have a value. Missing = domain invariant violated."
Nullable      →  "This field CAN be absent. Missing = valid domain state."
```

---

## Rules by Layer

### Layer 1: Entity & Aggregate Root

**Rule E-1: Non-nullable is the default. Every `?` must have a domain comment.**

```csharp
public class Order : AggregateRoot
{
    public Guid     BuyerId    { get; private set; }  // required — order cannot exist without buyer
    public Money    Total      { get; private set; }  // required — always has total
    public string   Reference  { get; private set; }  // required — generated on creation

    public string?           TrackingNumber { get; private set; }  // null = not shipped yet
    public DateTimeOffset?   ShippedAt      { get; private set; }  // null = not shipped yet
    public DateTimeOffset?   CompletedAt    { get; private set; }  // null = not completed
    public Guid?             CouponId       { get; private set; }  // null = no coupon applied
}
```

**Rule E-2: EF Core private constructor — DO NOT use `default!` on required fields.**

Use `#pragma warning disable CS8618` on the EF Core constructor only:

```csharp
public class Product : AggregateRoot
{
    public string Title { get; private set; }
    public string Slug  { get; private set; }
    public Money  Price { get; private set; }

#pragma warning disable CS8618 // Non-nullable field — EF Core uses this constructor
    private Product() { }
#pragma warning restore CS8618

    public static Product Create(string title, string slug, Money price)
    {
        Guard.NotNullOrEmpty(title, nameof(title));
        return new Product { Title = title, Slug = slug, Price = price };
    }
}
```

**Rule E-3: Collection properties are NEVER nullable — always initialize.**

```csharp
private readonly List<OrderLine> _lines = [];
public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
```

**Rule E-4: Nullable fields are only set via explicitly-named domain methods.**

```csharp
public Result<Unit, Error> MarkAsShipped(string trackingNumber)
{
    TrackingNumber = trackingNumber;
    ShippedAt      = DateTimeOffset.UtcNow;
    // ...
}
```

---

### Layer 2: Value Object

**Rule V-1: Value Objects NEVER have nullable properties.**

```csharp
public sealed record Money
{
    public decimal Amount { get; init; }
    public string Currency { get; init; }

    public Money(decimal amount, string currency) { /* validates + assigns */ }
}
```

**Rule V-2: Optional fields belong on the Entity, not the VO.**

**Rule V-3: Factory methods throw or return `Result<VO, Error>` — never return nullable VO.**

---

### Layer 3: DTO / Command / Query

**Rule D-1: Required field → non-nullable (use `required` keyword). Optional → nullable `?`.**

```csharp
public record NotificationItemDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? ActionUrl { get; init; }  // null = no action link
}
```

**Rule D-2: DO NOT use `string.Empty` as sentinel for "optional string".**

**Rule D-3: FluentValidation must guard nullable before validating content.**

```csharp
RuleFor(x => x.Description)
    .MaximumLength(2000)
    .When(x => x.Description is not null);
```

---

### Layer 4: EF Core Configuration

**Rule C-1: Non-nullable string → `.IsRequired()`. Nullable string → no annotation needed.**

**Rule C-2: Optional owned VO uses EF Core's nullable column handling automatically.**

---

## Quick Reference Table

| Layer | Non-nullable | Nullable | Never |
|-------|-------------|---------|-------|
| **Entity** | Required business fields | "Not yet happened" (ShippedAt), optional FK | `= default!`, `= null!`, nullable collection |
| **Value Object** | All properties | _(none)_ | Any `?` on properties |
| **DTO/Command** | Required input (use `required`) | Optional input/filter | `string.Empty` sentinel |
| **DTO/Response** | Always-present data | State-dependent data | _(no special restriction)_ |
| **EF Config** | `.IsRequired()` for strings | No `.IsRequired()` | `IsRequired(false)` on non-nullable |

---

## Banned Anti-patterns

```csharp
// ❌ 1. Null-forgiving operator to silence warnings
var name = user.DisplayName!;

// ❌ 2. default! on required fields
public string Title { get; private set; } = default!;

// ❌ 3. Nullable collections
public List<OrderLine>? Lines { get; set; }

// ❌ 4. Nullable in Value Object
public string? Currency { get; }

// ❌ 5. String.Empty as "no value" sentinel
public string TrackingNumber { get; private set; } = string.Empty;

// ❌ 6. Suppressing CS8602 with pragma
#pragma warning disable CS8602
```

---

## Allowed Exception

`Entity<TKey>.Id` uses `= default!` — this is the ONLY acceptable location because `TKey` is a generic
type parameter and EF Core / the base class handles ID assignment in all code paths.

---

## Review Checklist

```
Entity:
[ ] Every nullable field has a domain-reason comment
[ ] No = default! or = null! on required fields
[ ] Collections initialized with [] not null
[ ] Nullable fields set only via named domain methods
[ ] EF Core constructor uses #pragma warning disable CS8618

Value Object:
[ ] No nullable properties
[ ] Constructor validates and throws if invalid
[ ] Factory returns Result<VO, Error> if failure communication needed

DTO / Command / Query:
[ ] Required properties use `required` keyword
[ ] Optional properties use nullable `?`
[ ] No string.Empty sentinels
[ ] FluentValidation uses .When() for optional fields

EF Core Config:
[ ] Non-nullable strings have .IsRequired()
[ ] Nullable strings do NOT have .IsRequired()
[ ] Optional owned VOs configured correctly
```

