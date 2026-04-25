---
name: domain-model-review
description: >
  Quét toàn bộ Domain layer của MarketNest để review chất lượng DDD: aggregate design,
  value object immutability, domain event naming và placement, state machine transition,
  invariant enforcement, Result pattern usage, và phát hiện anemic domain model.
  Sử dụng skill này khi người dùng muốn: review domain model, kiểm tra DDD design,
  tìm anemic model, check aggregate boundary, review value object, kiểm tra domain event,
  review state machine, check invariant, hoặc nói bất kỳ cụm từ nào như "domain review",
  "DDD review", "aggregate design", "value object", "domain event", "anemic model",
  "invariant", "state machine", "Result pattern", "kiểm tra domain".
  Kích hoạt khi người dùng upload file trong Domain/ folder hoặc AggregateRoot subclass.
compatibility:
  tools: [bash, read_file, write_file, list_files]
  agents: [claude-code, gemini-cli, cursor, continue, aider]
  stack: [.NET 10, DDD, Clean Architecture, MediatR 12, MarketNest domain]
---

# Domain Model Review Skill — MarketNest

Skill này là deep-dive vào domain layer — lớp quan trọng nhất của MarketNest, nơi toàn bộ
business rules sống. Review theo 7 chiều DDD với reference trực tiếp đến invariants table
và aggregate designs đã được định nghĩa trong `domain-design.md`.

---

## Domain Map — MarketNest (reference nhanh)

```
Aggregates (9):
  Identity  → User, Role, RefreshToken
  Catalog   → Storefront, Product (+ ProductVariant, InventoryItem)
  Cart      → Cart (+ CartItem)                        [Redis TTL]
  Orders    → Order (+ OrderLine, Fulfillment)         [state machine: 9 status]
  Payments  → Payment (+ Commission, Payout)
  Reviews   → Review (+ ReviewVote, SellerReply)
  Disputes  → Dispute (+ DisputeMessage, Resolution)

Value Objects (Core):
  Money(amount, currency), Address, Rating(1-5), Sku, StorefrontSlug,
  ProductCategory, CartSnapshot, CartItemSnapshot

Domain Events (17 tổng):
  Storefront: Activated, Suspended, Closed
  Product: Published, Archived, InventoryLow, InventoryDepleted
  Cart: ItemAdded, ItemRemoved, CheckedOut, Abandoned
  Order: Placed, Confirmed, Shipped, Delivered, Completed, Cancelled, Disputed, Refunded
  Payment: Captured, Failed, Refunded
  Payout: Scheduled, Processed, ClawbackRequired
  Review: Submitted, Hidden, SellerReplyAdded
  Dispute: Opened, Escalated, Resolved

Business Invariants (10 không được vi phạm):
  1. QuantityAvailable ≥ 0
  2. Order total immutable after CONFIRMED
  3. Payout only for COMPLETED order
  4. Review only by buyer of COMPLETED order
  5. Dispute only within 3 days of DELIVERED
  6. Commission rate snapshotted at order time
  7. StorefrontSlug immutable after activation
  8. OrderLine price immutable after CONFIRMED
  9. Max 1 open dispute per Order
 10. Seller cannot modify prices after CONFIRMED
```

---

## Quy trình thực thi

```
Phase 1: SCAN    → Inventory domain files, aggregate tree
Phase 2: ANALYZE → 7 rule groups, đọc từng file cụ thể
Phase 3: REPORT  → BLOCKER / HIGH / MEDIUM / SUGGESTION với file:line
Phase 4: FIX     → Refactor code sẵn sàng (hỏi xác nhận trước khi apply)
Phase 5: VERIFY  → Chạy unit tests + architecture tests
```

---

## Phase 1: SCAN — Thu thập domain inventory

```bash
# 1A. Liệt kê tất cả Aggregate Root
echo "=== Aggregate Roots ==="
find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" | while read f; do
    if grep -q "AggregateRoot\|: AggregateRoot" "$f"; then
        echo "  $(basename $f .cs) — $f"
    fi
done

# 1B. Liệt kê tất cả Value Objects
echo "=== Value Objects ==="
find src/ -path "*/Domain/ValueObjects/*.cs" -path "*/ValueObjects/*.cs" \
  -not -path "*/bin/*" | while read f; do
    echo "  $(basename $f .cs) — $f"
done

# 1C. Liệt kê tất cả Domain Events
echo "=== Domain Events ==="
find src/ -path "*/Domain/Events/*.cs" -not -path "*/bin/*" | while read f; do
    classname=$(grep -oP "public record \K\w+" "$f" | head -1)
    echo "  $classname — $f"
done | sort

# 1D. Thống kê nhanh
echo "=== Metrics ==="
echo "Aggregate Roots: $(find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" \
  | xargs grep -l "AggregateRoot" | wc -l)"
echo "Value Objects:   $(find src/ \( -path "*/Domain/ValueObjects/*.cs" \
  -o -path "*/Core/ValueObjects/*.cs" \) -not -path "*/bin/*" | wc -l)"
echo "Domain Events:   $(find src/ -path "*/Domain/Events/*.cs" -not -path "*/bin/*" | wc -l)"
echo "Domain methods:  $(find src/ -path "*/Domain/*.cs" -not -path "*/bin/*" \
  | xargs grep -c "Result<\|public Result" 2>/dev/null | awk -F: '{sum+=$2} END{print sum}')"
```

---

## Phase 2: ANALYZE — 7 Rule Groups

---

### Rule Group 1: Aggregate Integrity — No Anemic Domain Model

**Quy tắc cốt lõi**: Aggregate root phải có private setters, mutations qua domain methods,
`static Create()` factory, và raise events từ bên trong method (không phải từ handler).

```bash
# 1A. Public setters trên Aggregate — anemic model killer
echo "=== PUBLIC SETTERS on Aggregates (anemic model) ==="
find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" | while read f; do
    # Find { get; set; } — public setter
    matches=$(grep -n "{ get; set; }" "$f" 2>/dev/null)
    if [ -n "$matches" ]; then
        echo "🔴 BLOCKER: $f"
        echo "$matches"
        echo ""
    fi
done

# 1B. Internal/protected setter (còn chấp nhận nhưng nên là private)
echo "=== Internal/protected setters on Aggregates ==="
find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" | while read f; do
    matches=$(grep -n "{ get; internal set; }\|{ get; protected set; }" "$f" 2>/dev/null)
    if [ -n "$matches" ]; then
        echo "🟡 MEDIUM: $f"
        echo "$matches"
    fi
done

# 1C. Aggregate thiếu static Create() factory method
echo "=== Aggregates missing static Create() factory ==="
find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" | while read f; do
    if grep -q "AggregateRoot" "$f"; then
        if ! grep -q "public static.*Create\b\|public static.*Register\b\|public static.*Open\b\|public static.*Submit\b" "$f"; then
            echo "⚠️  HIGH: $(basename $f) — no static factory method"
        fi
    fi
done

# 1D. Business logic nằm trong Handler thay vì Aggregate (anemic symptom)
echo "=== Business logic leaking into Application handlers ==="
find src/ -path "*/Application/Commands/*.cs" -not -path "*/bin/*" | while read f; do
    # Flag nếu handler có nhiều business logic (if-else chains, calculations)
    if_count=$(grep -c "if\s*(" "$f" 2>/dev/null || echo 0)
    if [ "$if_count" -gt 5 ]; then
        echo "⚠️  MEDIUM: $f has $if_count if-branches — business logic should be in Aggregate"
    fi
done

# 1E. Direct property assignment on aggregate from handler
echo "=== Direct state mutation on aggregates from handlers ==="
find src/ -path "*/Application/*.cs" -not -path "*/bin/*" | xargs grep -hn \
    "\.\(Status\|Total\|Amount\|BuyerId\|SellerId\|ShippedAt\|DeliveredAt\)\s*=" \
    2>/dev/null | grep -v "bin/\|obj/\|//\|private\|==" | head -20
```

**Anatomy của Aggregate chuẩn — MarketNest template:**
```csharp
// ✅ Chuẩn: Orders/Domain/Entities/Order.cs
public sealed class Order : AggregateRoot          // ← sealed: prevent inheritance
{
    // ── Identity ────────────────────────────────────────────────────────
    public Guid BuyerId     { get; private set; }  // ← private setter
    public OrderStatus Status { get; private set; }
    public Money Total      { get; private set; }
    public Address ShippingAddress { get; private set; } = null!;

    // ── Child collections — encapsulated ────────────────────────────────
    private readonly List<OrderLine> _lines = [];
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly(); // ← read-only projection

    private readonly List<Fulfillment> _fulfillments = [];
    public IReadOnlyList<Fulfillment> Fulfillments => _fulfillments.AsReadOnly();

    // ── Private constructor — force use of factory ───────────────────────
    private Order() { }  // EF Core needs this — private, not public

    // ── Static factory — only way to create ─────────────────────────────
    public static Result<Order, Error> Create(CartSnapshot cart)
    {
        if (cart.Items.Count == 0)
            return Errors.Order.EmptyCart;

        var order = new Order
        {
            Id              = Guid.NewGuid(),
            BuyerId         = cart.BuyerId,
            Status          = OrderStatus.Pending,
            ShippingAddress = cart.ShippingAddress,
            PlacedAt        = DateTime.UtcNow
        };

        foreach (var item in cart.Items)
            order._lines.Add(OrderLine.Create(order.Id, item));

        order.Total = Money.Of(order._lines.Sum(l => l.LineTotal.Amount));

        // ← Event raised from inside factory
        order.AddDomainEvent(new OrderPlacedEvent(
            order.Id, cart.BuyerId,
            order._lines.Select(l => l.ToEventDto()).ToList(),
            order.Total));

        return order;
    }

    // ── Domain methods — ALL mutations go through here ───────────────────
    public Result<Unit, Error> MarkAsShipped(string trackingNumber)
    {
        // ← Guard: enforce valid transition
        if (Status != OrderStatus.Confirmed)
            return Errors.Order.InvalidTransition(Status, OrderStatus.Shipped);

        // ← Guard: enforce business rule
        if (string.IsNullOrWhiteSpace(trackingNumber))
            return Errors.Order.TrackingNumberRequired;

        // ← State mutation: ONLY inside domain method
        Status    = OrderStatus.Shipped;
        ShippedAt = DateTime.UtcNow;

        // ← Event raised from domain method, not handler
        AddDomainEvent(new OrderShippedEvent(Id, BuyerId, trackingNumber));
        return Result.Success();
    }

    public Result<Unit, Error> OpenDispute(Guid buyerId, DisputeReason reason)
    {
        // ← Multiple invariant guards
        if (Status != OrderStatus.Delivered)
            return Errors.Order.NotEligibleForDispute;

        if (DeliveredAt is null || DateTime.UtcNow > DeliveredAt.Value.AddDays(3))
            return Errors.Order.DisputeWindowClosed;     // ← Invariant #5

        Status = OrderStatus.Disputed;
        AddDomainEvent(new OrderDisputedEvent(Id, buyerId, reason));
        return Result.Success();
    }
}

// ❌ Anemic Domain Model — tất cả đều là data container
public class Order : AggregateRoot
{
    public OrderStatus Status { get; set; }   // public setter!
    public Money Total { get; set; }          // anyone can mutate
    // No domain methods, no events, no invariants
}
```

---

### Rule Group 2: Value Object Immutability & Correctness

**Quy tắc**: Value objects phải là `record` hoặc inherit `ValueObject`. Không có `Id`, không mutable,
validate trong constructor, structural equality tự động.

```bash
# 2A. Value object có public setter (sai — phải immutable)
echo "=== Mutable Value Objects ==="
find src/ -path "*/ValueObjects/*.cs" -not -path "*/bin/*" | while read f; do
    if grep -q "{ get; set; }" "$f"; then
        echo "🔴 BLOCKER: $f — Value Object has mutable property"
        grep -n "{ get; set; }" "$f"
    fi
done

# 2B. Value object có Id property (value objects không có identity)
echo "=== Value Objects with Id (should have no identity) ==="
find src/ -path "*/ValueObjects/*.cs" -not -path "*/bin/*" | xargs grep -ln "\bId\b\|\.Id\b" \
  2>/dev/null | grep -v "bin/\|obj/"

# 2C. Value object không validate trong constructor
echo "=== Value Objects without constructor validation ==="
find src/ -path "*/ValueObjects/*.cs" -not -path "*/bin/*" | while read f; do
    if ! grep -q "throw\|DomainException\|if.*<\|if.*>\|if.*IsNullOrEmpty\|if.*Length" "$f"; then
        echo "⚠️  MEDIUM: $(basename $f) — no validation in constructor"
    fi
done

# 2D. Value object không inherit ValueObject base (missing equality)
echo "=== Value Objects not inheriting ValueObject base ==="
find src/ -path "*/ValueObjects/*.cs" -not -path "*/bin/*" | while read f; do
    if ! grep -q ": ValueObject\|record " "$f"; then
        echo "⚠️  HIGH: $f — not a record and doesn't inherit ValueObject"
    fi
done

# 2E. Tìm primitive obsession — nên là Value Object
echo "=== Primitive Obsession candidates ==="
find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" \
  | xargs grep -hn "public string.*Price\|public decimal.*Price\|public string.*Email\|public string.*Phone\|public string.*Slug\b" \
  2>/dev/null | grep -v "//\|bin/\|obj/" | head -20
# Candidates: Email, Phone, Slug, Price (raw decimal) → nên là Value Object
```

**Value Object templates chuẩn:**

```csharp
// ── Pattern 1: C# record (preferred — equality built-in) ────────────────────

// ✅ Money — pure record, immutable
public record Money
{
    public decimal Amount   { get; }   // init-only via constructor
    public string  Currency { get; }

    // ← Private constructor + static factory for validation
    private Money(decimal amount, string currency)
    {
        Amount   = amount;
        Currency = currency;
    }

    public static Money Of(decimal amount, string currency = "SGD")
    {
        if (amount < 0)
            throw new DomainException("Money amount cannot be negative");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new DomainException("Currency must be a 3-letter ISO code");
        return new Money(amount, currency.ToUpperInvariant());
    }

    // ← Domain operations on the value
    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException($"Cannot add {Currency} and {other.Currency}");
        return Of(Amount + other.Amount, Currency);
    }

    public Money Multiply(int factor) => Of(Amount * factor, Currency);

    public bool IsGreaterThan(Money other)
    {
        if (Currency != other.Currency)
            throw new DomainException("Cannot compare different currencies");
        return Amount > other.Amount;
    }

    // ← record auto-generates structural equality: new Money(10, "SGD") == new Money(10, "SGD")
    public override string ToString() => $"{Currency} {Amount:F2}";
}

// ✅ Rating — simple constrained integer
public record Rating
{
    public int Value { get; }

    public Rating(int value)
    {
        if (value is < 1 or > 5)
            throw new DomainException($"Rating must be 1-5, got {value}");
        Value = value;
    }

    public static Rating Of(int value) => new(value);
}

// ── Pattern 2: ValueObject base class (for complex equality scenarios) ────────

// ✅ Address — inherits ValueObject for custom equality
public class Address : ValueObject
{
    public string RecipientName { get; }
    public string Street        { get; }
    public string? Street2      { get; }
    public string City          { get; }
    public string StateProvince { get; }
    public string PostalCode    { get; }
    public string CountryCode   { get; }  // ISO 3166-1 alpha-2

    public Address(string recipientName, string street, string? street2,
                   string city, string stateProvince, string postalCode, string countryCode)
    {
        if (string.IsNullOrWhiteSpace(recipientName))
            throw new DomainException("Recipient name is required");
        if (string.IsNullOrWhiteSpace(street))
            throw new DomainException("Street is required");
        if (countryCode?.Length != 2)
            throw new DomainException("Country code must be ISO 3166-1 alpha-2 (2 chars)");

        RecipientName = recipientName;
        Street        = street;
        Street2       = street2;
        City          = city;
        StateProvince = stateProvince;
        PostalCode    = postalCode;
        CountryCode   = countryCode!.ToUpperInvariant();
    }

    // ← ValueObject base requires this for structural equality
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return RecipientName.ToLowerInvariant();
        yield return Street.ToLowerInvariant();
        yield return Street2?.ToLowerInvariant();
        yield return City.ToLowerInvariant();
        yield return PostalCode.ToUpperInvariant();
        yield return CountryCode;
    }

    public string OneLine =>
        $"{RecipientName}, {Street}, {City}, {StateProvince} {PostalCode}, {CountryCode}";
}

// ❌ Primitive obsession — missing validation, no domain operations
public class Product : AggregateRoot
{
    public decimal Price { get; private set; }  // raw decimal — no currency, no validation
    public string Slug { get; set; }            // mutable! should be Value Object
}
```

---

### Rule Group 3: Domain Event Naming & Placement

**Quy tắc**: Events phải past tense, mang đủ data để consumers xử lý độc lập,
raised từ aggregate method (không phải handler), và implement `IDomainEvent`.

```bash
# 3A. Event naming — phải past tense (OrderPlaced, not PlaceOrder, not OrderPlace)
echo "=== Domain Event naming violations ==="
find src/ -path "*/Domain/Events/*.cs" -not -path "*/bin/*" | while read f; do
    classname=$(grep -oP "(?<=public record |public class )\w+" "$f" | head -1)
    if [ -z "$classname" ]; then continue; fi

    # Events phải kết thúc bằng "Event"
    if ! echo "$classname" | grep -qE "Event$"; then
        echo "🔴 BLOCKER: $classname in $f — must end with 'Event'"
    fi

    # Events phải có past-tense verb (Placed, Confirmed, Shipped, not Place, Confirm, Ship)
    # Heuristic: tìm các tên có verb không ở past tense
    if echo "$classname" | grep -qE "(Create|Update|Delete|Process|Ship|Cancel|Open|Close)(d)?Event$"; then
        if ! echo "$classname" | grep -qE "(Created|Updated|Deleted|Processed|Shipped|Cancelled|Opened|Closed)Event$"; then
            echo "🟡 MEDIUM: $classname — verb should be past tense"
        fi
    fi
done

# 3B. Event không implement IDomainEvent
echo "=== Events not implementing IDomainEvent ==="
find src/ -path "*/Domain/Events/*.cs" -not -path "*/bin/*" | while read f; do
    if grep -q "Event\b" "$f"; then
        if ! grep -q "IDomainEvent\|INotification" "$f"; then
            echo "🔴 BLOCKER: $f — must implement IDomainEvent"
        fi
    fi
done

# 3C. Event raised từ Application handler (sai — phải từ Aggregate)
echo "=== Domain events raised outside Aggregate ==="
find src/ -path "*/Application/Commands/*.cs" -not -path "*/bin/*" \
  | xargs grep -hn "AddDomainEvent\|new.*Event(" 2>/dev/null \
  | grep -v "bin/\|obj/\|//\|builder\|test" | head -20

# 3D. Event thiếu dữ liệu cần thiết (consumer cần biết gì?)
echo "=== Events with potentially insufficient data ==="
find src/ -path "*/Domain/Events/*.cs" -not -path "*/bin/*" | while read f; do
    # Count constructor parameters — events với < 2 params có thể thiếu context
    params=$(grep -oP "(?<=record \w{1,50}\()[^)]*" "$f" | tr ',' '\n' | grep -c "\S" 2>/dev/null || echo 0)
    if [ "$params" -lt 2 ] && grep -q "record.*Event" "$f"; then
        echo "⚠️  SUGGESTION: $(basename $f .cs) has only $params param — may need more context"
    fi
done

# 3E. Domain events đặt nhầm folder
echo "=== Event files outside Domain/Events/ folder ==="
find src/ -name "*Event.cs" -not -path "*/Events/*" \
  -not -path "*/bin/*" -not -path "*/obj/*" \
  -not -path "*/tests/*" | head -10
```

**Fix patterns — Domain Event chuẩn:**

```csharp
// ✅ Domain Event đủ data — consumers không cần query thêm
// Orders/Domain/Events/OrderPlacedEvent.cs
public record OrderPlacedEvent(
    Guid              OrderId,
    Guid              BuyerId,
    Guid              SellerId,          // ← Notifications cần gửi email cho Seller
    IReadOnlyList<OrderLineEventDto> Lines, // ← Payments cần tính commission
    Money             Total,
    DateTime          OccurredAt = default) : IDomainEvent
{
    public Guid     EventId    { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = OccurredAt == default ? DateTime.UtcNow : OccurredAt;
}

// DTO nhỏ trong event — không expose domain entity
public record OrderLineEventDto(Guid VariantId, Guid StoreId, Money UnitPrice, int Quantity);

// ❌ Event thiếu data — consumer phải query thêm (N+1 problem)
public record OrderPlacedEvent(Guid OrderId) : IDomainEvent;
// → Payments handler phải fetch Order để lấy Total → coupling + extra query

// ❌ Event tên sai — present tense
public record PlaceOrderEvent(...) : IDomainEvent; // "Place" not past tense
public record OrderPlace(...) : IDomainEvent;      // not a past-tense verb form

// ✅ Tất cả events của MarketNest phải follow:
// [Aggregate][PastTenseVerb]Event
// OrderPlacedEvent, OrderShippedEvent, ReviewSubmittedEvent, DisputeOpenedEvent
```

---

### Rule Group 4: State Machine Transitions

**MarketNest Order State Machine — 9 trạng thái, quy tắc chặt:**
```
Valid transitions:
  Pending     → Confirmed   (payment captured)
  Confirmed   → Processing  (seller confirms)
  Confirmed   → Cancelled   (seller cancels / timeout 48h)
  Processing  → Shipped     (seller ships, tracking required)
  Shipped     → Delivered   (buyer confirms / auto 30d)
  Delivered   → Completed   (no dispute / auto 3d)
  Delivered   → Disputed    (buyer opens dispute within 3d)
  Disputed    → Completed   (admin resolves: seller wins)
  Disputed    → Refunded    (admin resolves: buyer wins)

All other transitions: → InvalidTransition error
```

```bash
# 4A. Tìm state machine không dùng switch expression (error-prone)
echo "=== State machine not using switch expression ==="
find src/ -path "*/Domain/*.cs" -not -path "*/bin/*" \
  | xargs grep -ln "if.*Status.*==\|if.*status.*==" 2>/dev/null \
  | xargs grep -l "Transition\|transition\|Status\s*=" 2>/dev/null | head -10

# 4B. State transition không return Result (throw exception thay vì Result)
echo "=== State transitions throwing exceptions instead of returning Result ==="
find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" \
  | xargs grep -hn "throw new.*Exception\|throw.*DomainException" 2>/dev/null \
  | grep -v "ValueObject\|bin/\|obj/\|//\|private\|constructor" | head -20
# Domain methods nên return Result<Unit, Error> thay vì throw
# Exception dùng cho invariant violations trong constructor/VO, không phải state transitions

# 4C. Missing timestamp update khi transition
echo "=== State transitions potentially missing timestamp ==="
find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" | while read f; do
    # Tìm Status assignment mà không có timestamp assignment gần đó
    grep -n "Status\s*=\s*" "$f" | while read line; do
        lineno=$(echo "$line" | cut -d: -f1)
        context=$(sed -n "${lineno},$((lineno+3))p" "$f")
        if ! echo "$context" | grep -q "At\s*=\|At =\|DateTime"; then
            echo "⚠️  MEDIUM: $f:$lineno — Status change without timestamp"
        fi
    done
done | head -20

# 4D. Auto-transition background job — check có domain method matching không
echo "=== Auto-transition jobs ==="
find src/ -path "*/Infrastructure/Jobs/*.cs" -not -path "*/bin/*" | while read f; do
    echo "  Reading: $f"
    grep -n "Status\s*=\|\.Complete\b\|\.Deliver\b\|\.Cancel\b" "$f" | head -5
done
```

**Fix — State machine chuẩn với switch expression:**

```csharp
// ✅ Switch expression state machine — exhaustive, readable
public sealed class Order : AggregateRoot
{
    // ── Single entry point for all transitions ───────────────────────────
    public Result<Unit, Error> Transition(OrderStatus target) =>
        (Status, target) switch
        {
            (OrderStatus.Pending,    OrderStatus.Confirmed)   => Confirm(),
            (OrderStatus.Confirmed,  OrderStatus.Processing)  => Process(),
            (OrderStatus.Confirmed,  OrderStatus.Cancelled)   => Cancel("Seller cancelled"),
            (OrderStatus.Processing, OrderStatus.Shipped)     => Ship_RequiresTracking(), // guard
            (OrderStatus.Shipped,    OrderStatus.Delivered)   => Deliver(),
            (OrderStatus.Delivered,  OrderStatus.Completed)   => Complete(),
            (OrderStatus.Delivered,  OrderStatus.Disputed)    => Dispute_RequiresBuyer(), // guard
            (OrderStatus.Disputed,   OrderStatus.Completed)   => CompleteAfterDispute(),
            (OrderStatus.Disputed,   OrderStatus.Refunded)    => Refund(),
            _ => Errors.Order.InvalidTransition(Status, target)
        };

    // ── Individual transition methods — each enforces its own invariants ──
    private Result<Unit, Error> Confirm()
    {
        Status      = OrderStatus.Confirmed;
        ConfirmedAt = DateTime.UtcNow;
        AddDomainEvent(new OrderConfirmedEvent(Id, BuyerId, SellerId));
        return Result.Success();
    }

    // Overload with required parameter — not in switch, called directly
    public Result<Unit, Error> MarkAsShipped(string trackingNumber)
    {
        if (Status != OrderStatus.Confirmed)
            return Errors.Order.InvalidTransition(Status, OrderStatus.Shipped);
        if (string.IsNullOrWhiteSpace(trackingNumber))
            return Errors.Order.TrackingNumberRequired;

        Status          = OrderStatus.Shipped;
        ShippedAt       = DateTime.UtcNow;
        _trackingNumber = trackingNumber;
        AddDomainEvent(new OrderShippedEvent(Id, BuyerId, trackingNumber));
        return Result.Success();
    }

    public Result<Unit, Error> OpenDispute(Guid buyerId, DisputeReason reason,
                                            IDateTimeService clock)
    {
        if (Status != OrderStatus.Delivered)
            return Errors.Order.NotEligibleForDispute;

        // ← Invariant #5: within 3 days of DELIVERED
        if (DeliveredAt is null || clock.UtcNow > DeliveredAt.Value.AddDays(3))
            return Errors.Order.DisputeWindowClosed;

        Status = OrderStatus.Disputed;
        AddDomainEvent(new OrderDisputedEvent(Id, buyerId, reason));
        return Result.Success();
    }

    private Result<Unit, Error> Complete()
    {
        Status      = OrderStatus.Completed;
        CompletedAt = DateTime.UtcNow;

        // ← Invariant #3: Payout only for COMPLETED
        var payoutAmount = Total.Multiply(1 - CommissionRate);
        AddDomainEvent(new OrderCompletedEvent(Id, BuyerId, SellerId, payoutAmount));
        return Result.Success();
    }
}
```

---

### Rule Group 5: Invariant Enforcement

**Quy tắc**: 10 business invariants phải được enforce trong domain method,
không phải Application layer. Mỗi invariant phải có error code riêng.

```bash
# 5A. Kiểm tra các invariant có được enforce trong Domain không
echo "=== Invariant enforcement check ==="

# Invariant #2: Order total immutable after CONFIRMED
grep -rn "Total\s*=\|_total\s*=" src/ -path "*/Domain/*.cs" \
  | grep -v "private\s*readonly\|= Money\|= null!\|bin/\|obj/\|//\|Create\b" | head -10
# Expect: Total chỉ được set trong Create() method

# Invariant #5: Dispute window 3 days
grep -rn "AddDays(3)\|DisputeWindow\|dispute.*window\|3.*day.*dispute" \
  src/ --include="*.cs" | grep -v "bin/\|obj/\|//\|test" | head -5
# Expect: check này phải nằm trong Order.OpenDispute() hoặc Dispute.Create()

# Invariant #7: StorefrontSlug immutable after activation
grep -rn "Slug\s*=\|_slug\s*=" src/ -path "*/Catalog/Domain/*.cs" \
  | grep -v "private\|= StorefrontSlug\|= null!\|bin/\|obj/\|//" | head -10
# Expect: Slug chỉ được set 1 lần trong constructor

# Invariant #8: OrderLine price immutable after CONFIRMED
grep -rn "UnitPrice\s*=\|_unitPrice\s*=" src/ -path "*/Orders/Domain/*.cs" \
  | grep -v "private\|= Money\|= null!\|bin/\|obj/\|//" | head -5

# 5B. Tìm invariant đang enforce trong Application layer thay vì Domain
echo "=== Invariants potentially misplaced in Application layer ==="
find src/ -path "*/Application/Commands/*.cs" -not -path "*/bin/*" | while read f; do
    # Flag: handler check business conditions mà nên ở domain
    issues=$(grep -n "\.Status ==\|\.Count >=\|\.Count >\|AddDays\|DateTime.*>\|DateTime.*<" "$f" \
      | grep -v "//\|result\.\|Result\." | head -3)
    if [ -n "$issues" ]; then
        echo "⚠️  HIGH: $f — business rule in handler (should be in Domain):"
        echo "$issues"
        echo ""
    fi
done

# 5C. Error codes không theo convention DOMAIN.ENTITY_ERROR
echo "=== Error codes not following DOMAIN.ENTITY_ERROR convention ==="
grep -rn "new Error(\|Error\.Conflict\|Error\.NotFound" src/ -path "*/Domain/*.cs" \
  --include="*.cs" | grep -v "bin/\|obj/\|//" | head -20
# Expect: tất cả dạng "ORDER.NOT_FOUND", "CART.INSUFFICIENT_STOCK", v.v.

# 5D. Errors static class — kiểm tra có đủ các error codes không
echo "=== Errors static class coverage ==="
find src/ -name "Errors.cs" -not -path "*/bin/*"
# Mỗi module domain nên có Errors.cs riêng hoặc dùng static inner class
```

**Fix — Invariant enforcement patterns:**

```csharp
// ── Invariant #2: Order total immutable ─────────────────────────────────────

// ✅ Đúng: Total chỉ set trong Create()
public sealed class Order : AggregateRoot
{
    public Money Total { get; private set; } = null!; // private setter

    public static Result<Order, Error> Create(CartSnapshot cart)
    {
        var order = new Order();
        order.Total = Money.Of(cart.Items.Sum(i => i.UnitPrice.Amount * i.Quantity));
        // After this, Total is NEVER set again
        return order;
    }
    // NO method ever sets Total again after Create()
}

// ── Invariant #7: StorefrontSlug immutable after activation ─────────────────

// ✅ Đúng: Slug locked via domain guard
public sealed class Storefront : AggregateRoot
{
    public StorefrontSlug Slug   { get; private set; } = null!;
    public StorefrontStatus Status { get; private set; }

    public Result<Unit, Error> Activate()
    {
        if (Status != StorefrontStatus.Draft)
            return Errors.Storefront.AlreadyActive;
        // After activation: Slug is LOCKED — no method changes it
        Status      = StorefrontStatus.Active;
        ActivatedAt = DateTime.UtcNow;
        AddDomainEvent(new StorefrontActivatedEvent(Id, SellerId, Slug.Value));
        return Result.Success();
    }

    // ✅ Slug change method guards against post-activation change
    public Result<Unit, Error> ChangeSlug(StorefrontSlug newSlug)
    {
        if (Status == StorefrontStatus.Active)
            return Errors.Storefront.SlugImmutableAfterActivation; // Invariant #7
        Slug = newSlug;
        return Result.Success();
    }
}

// ── Errors static class — chuẩn hóa error codes ─────────────────────────────

// ✅ Orders/Domain/Errors.cs (hoặc static inner trong Order.cs)
public static class Errors
{
    public static class Order
    {
        // NotFound
        public static Error NotFound(Guid id)
            => new("ORDER.NOT_FOUND", $"Order {id} was not found", ErrorType.NotFound);

        // State transition errors
        public static Error InvalidTransition(OrderStatus from, OrderStatus to)
            => new("ORDER.INVALID_TRANSITION",
                   $"Cannot transition from {from} to {to}", ErrorType.Conflict);

        // Business invariants
        public static readonly Error DisputeWindowClosed
            = new("ORDER.DISPUTE_WINDOW_CLOSED",
                  "Dispute window has expired (3 days after delivery)", ErrorType.Conflict);

        public static readonly Error EmptyCart
            = new("ORDER.EMPTY_CART",
                  "Cannot create order from empty cart", ErrorType.Validation);

        public static readonly Error TrackingNumberRequired
            = new("ORDER.TRACKING_REQUIRED",
                  "Tracking number is required when shipping", ErrorType.Validation);
    }

    public static class Cart
    {
        public static readonly Error MaxItemsReached
            = new("CART.MAX_ITEMS", "Cart cannot have more than 20 distinct items", ErrorType.Conflict);

        public static readonly Error CheckedOut
            = new("CART.ALREADY_CHECKED_OUT", "Cannot modify a checked-out cart", ErrorType.Conflict);

        public static readonly Error InsufficientStock
            = new("CART.INSUFFICIENT_STOCK", "Not enough stock available", ErrorType.Conflict);
    }

    public static class Storefront
    {
        public static readonly Error SlugImmutableAfterActivation
            = new("STOREFRONT.SLUG_IMMUTABLE",
                  "Storefront slug cannot be changed after activation", ErrorType.Conflict);
    }
}
```

---

### Rule Group 6: Result Pattern Consistency

**Quy tắc**: Domain methods trả về `Result<T, Error>` (không throw exception cho business failures).
Exception chỉ dùng cho invariant violations trong Value Object constructor.
Error codes phải theo `DOMAIN.ENTITY_ERROR` convention.

```bash
# 6A. Domain method throw exception thay vì return Result
echo "=== Domain methods throwing business exceptions (should return Result) ==="
find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" \
  | xargs grep -hn "throw new.*Exception\|throw new Domain" 2>/dev/null \
  | grep -v "bin/\|obj/\|//" | head -20
# OK: throw trong ValueObject constructor
# NOT OK: throw trong domain methods (MarkAsShipped, Complete, OpenDispute)

# 6B. Result không được unwrap an toàn (direct .Value access)
echo "=== Unsafe Result unwrap (.Value without IsSuccess check) ==="
find src/ --include="*.cs" -not -path "*/bin/*" \
  | xargs grep -hn "\.Value\b" 2>/dev/null \
  | grep -v "//\|IsSuccess\|if.*Value\|Value =\|HasValue\|bin/\|obj/\|test\|Test" | head -20

# 6C. Tìm nơi bỏ qua Result return value
echo "=== Discarded Result values ==="
find src/ -path "*/Application/*.cs" -not -path "*/bin/*" \
  | xargs grep -hn "order\.\|cart\.\|dispute\.\|review\." 2>/dev/null \
  | grep -E "\.(MarkAs|Complete|Cancel|Open|Submit|Confirm|Ship|Deliver)\(" \
  | grep -v "var \|await \|result\|Result\|return\|=\|bin/\|obj/" | head -20
# var result = order.Complete() — ok
# order.Complete() — result bị bỏ qua!

# 6D. Handler không xử lý failure case của Result
echo "=== Handlers not handling failure path ==="
find src/ -path "*/Application/Commands/*.cs" -not -path "*/bin/*" | while read f; do
    if grep -q "\.Match\b\|\.IsFailure\b\|\.IsSuccess\b\|if.*result" "$f"; then
        : # Has result handling
    else
        # Check if file calls domain methods
        if grep -qE "\.(MarkAs|Complete|Cancel|Open|Submit|Create)\(" "$f"; then
            echo "⚠️  HIGH: $f — calls domain methods but may not handle Result"
        fi
    fi
done

# 6E. Map() và MapAsync() — kiểm tra chain Result đúng không
echo "=== Result chaining patterns ==="
grep -rn "\.Map\b\|\.MapAsync\b\|\.Bind\b" src/ --include="*.cs" \
  | grep -v "bin/\|obj/\|//\|test\|Test" | head -10
```

**Fix — Result pattern chuẩn:**

```csharp
// ── Domain method: return Result, không throw ────────────────────────────────

// ❌ Sai: throw trong domain method
public void MarkAsShipped(string trackingNumber)
{
    if (Status != OrderStatus.Confirmed)
        throw new DomainException("Invalid transition"); // callers must try/catch!
    Status = OrderStatus.Shipped;
}

// ✅ Đúng: return Result
public Result<Unit, Error> MarkAsShipped(string trackingNumber)
{
    if (Status != OrderStatus.Confirmed)
        return Errors.Order.InvalidTransition(Status, OrderStatus.Shipped);
    if (string.IsNullOrWhiteSpace(trackingNumber))
        return Errors.Order.TrackingNumberRequired;

    Status    = OrderStatus.Shipped;
    ShippedAt = DateTime.UtcNow;
    AddDomainEvent(new OrderShippedEvent(Id, BuyerId, trackingNumber));
    return Result.Success();
}

// ── Handler: xử lý đầy đủ cả success và failure path ───────────────────────

// ❌ Sai: bỏ qua Result
public async Task<Result<Unit, Error>> Handle(ShipOrderCommand cmd, CancellationToken ct)
{
    var order = await _repo.GetByIdOrThrowAsync(cmd.OrderId, ct);
    order.MarkAsShipped(cmd.TrackingNumber); // ← Result bị bỏ qua!
    await _repo.SaveChangesAsync(ct);
    return Result.Success();
}

// ✅ Đúng: handle both paths
public async Task<Result<Unit, Error>> Handle(ShipOrderCommand cmd, CancellationToken ct)
{
    var order = await _repo.GetByIdOrThrowAsync(cmd.OrderId, ct);

    var result = order.MarkAsShipped(cmd.TrackingNumber);
    if (result.IsFailure) return result; // ← early return with error

    await _repo.SaveChangesAsync(ct);
    return Result.Success();
}

// ── Result chaining — Map / MapAsync ─────────────────────────────────────────

// ✅ Chain multiple operations cleanly
var result = await cartService
    .GetActiveCartAsync(cmd.BuyerId, ct)          // Result<Cart, Error>
    .MapAsync(cart => cart.Checkout())            // Result<CartSnapshot, Error>
    .MapAsync(snapshot => Order.Create(snapshot)) // Result<Order, Error>
    .TapAsync(order => _orderRepo.Add(order))     // side effect
    .MapAsync(order => order.Id);                 // Result<Guid, Error>

// ── Endpoint: Match trên Result ───────────────────────────────────────────────

// ✅ Minimal API
return result.Match(
    orderId  => TypedResults.Created($"/api/orders/{orderId}", new { id = orderId }),
    error    => error.ToTypedProblem()
);

// ✅ PageModel
return result.Match(
    orderId  => RedirectToPage("/Account/Orders/Confirmation", new { id = orderId }),
    error    => { ModelState.AddModelError("", error.Message); return Page(); }
);
```

---

### Rule Group 7: Aggregate Boundary & Child Entity Design

**Quy tắc**: Child entities chỉ được truy cập qua Aggregate Root.
Không có public collection (dùng `IReadOnlyList`). Không reference entity khác aggregate bằng nav prop — dùng ID.

```bash
# 7A. Public collection (bypass aggregate boundary)
echo "=== Public mutable collections on Aggregates ==="
find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" \
  | xargs grep -hn "public List<\|public IList<\|public ICollection<\|public HashSet<" \
  2>/dev/null | grep -v "bin/\|obj/\|//" | head -20
# Expect: chỉ IReadOnlyList<>, không có List<> public

# 7B. Nav prop cross-aggregate (nên dùng ID)
echo "=== Cross-aggregate navigation properties ==="
# Orders không nên có nav prop đến Cart, chỉ có CartId
grep -rn "public.*Cart\b\|public.*Storefront\b\|public.*User\b" \
  src/ -path "*/Orders/Domain/*.cs" --include="*.cs" \
  | grep -v "Id\b\|Snapshot\|//\|bin/\|obj/" | head -10

grep -rn "public.*Order\b\|public.*Review\b" \
  src/ -path "*/Payments/Domain/*.cs" --include="*.cs" \
  | grep -v "Id\b\|//\|bin/\|obj/" | head -10

# 7C. Child entity được tạo từ bên ngoài aggregate
echo "=== Child entities instantiated outside Aggregate ==="
grep -rn "new OrderLine\|new CartItem\|new DisputeMessage\|new OrderLine\|new Fulfillment" \
  src/ -path "*/Application/*.cs" --include="*.cs" \
  | grep -v "bin/\|obj/\|//\|test\|Test" | head -10
# Expect: Child entities chỉ được create trong Aggregate method

# 7D. Child entity có public constructor (bypass)
echo "=== Child entities with public constructor ==="
find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" | while read f; do
    classname=$(grep -oP "public class \K\w+" "$f" | head -1)
    if [ -n "$classname" ] && ! grep -q "AggregateRoot" "$f"; then
        # Check for public constructor
        if grep -q "public $classname(" "$f"; then
            echo "⚠️  MEDIUM: $f — child entity '$classname' has public constructor"
        fi
    fi
done
```

**Fix — Aggregate boundary patterns:**

```csharp
// ── Child entity: internal constructor + static factory via Aggregate ─────────

// ✅ OrderLine — only Aggregate can create it
public sealed class OrderLine   // NOT AggregateRoot — it's a child entity
{
    public Guid  OrderLineId { get; private set; }
    public Guid  OrderId     { get; private set; }
    public Guid  VariantId   { get; private set; }
    public string ProductTitle { get; private set; } = null!;
    public Money UnitPrice   { get; private set; } = null!;  // ← immutable after CONFIRMED (#8)
    public int   Quantity    { get; private set; }
    public Money LineTotal   => UnitPrice.Multiply(Quantity); // ← computed, not stored

    // ✅ Internal constructor — only Order.Create() can use this
    internal static OrderLine Create(Guid orderId, CartItemSnapshot item)
        => new OrderLine
        {
            OrderLineId  = Guid.NewGuid(),
            OrderId      = orderId,
            VariantId    = item.VariantId,
            ProductTitle = item.ProductTitle,        // ← snapshot at order time
            UnitPrice    = item.UnitPrice,           // ← snapshot at order time
            Quantity     = item.Quantity,
        };

    private OrderLine() { } // EF Core only
}

// ✅ Order: encapsulated collection
public sealed class Order : AggregateRoot
{
    private readonly List<OrderLine> _lines = [];
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly(); // ← read-only!

    // ← Cross-aggregate: ID reference, not nav prop
    public Guid CartId { get; private set; }      // ✅ ID only
    // ❌ public Cart Cart { get; set; }           // Nav prop = tight coupling

    // ← Child entity created inside aggregate method
    public static Result<Order, Error> Create(CartSnapshot cart)
    {
        var order = new Order { CartId = cart.CartId };
        foreach (var item in cart.Items)
            order._lines.Add(OrderLine.Create(order.Id, item)); // ← only here!
        return order;
    }
}
```

---

## Phase 3: REPORT — Domain Model Quality Report

```markdown
# Domain Model Review Report — MarketNest
**Date**: <ngày>
**Aggregates reviewed**: X / 7
**Value Objects reviewed**: X
**Domain Events reviewed**: X / 17

---

## Tổng quan chất lượng

| Chiều | Score (1–10) | Findings |
|---|---|---|
| Aggregate Integrity / No Anemic | X/10 | X public setters, X missing factory |
| Value Object Immutability | X/10 | X mutable VOs, X primitive obsession |
| Domain Event Naming & Placement | X/10 | X naming issues, X events in handler |
| State Machine Correctness | X/10 | X missing transitions, X no timestamp |
| Invariant Enforcement | X/10 | X invariants in App layer |
| Result Pattern Consistency | X/10 | X throw instead of Result |
| Aggregate Boundary | X/10 | X public collections, X cross-aggregate nav |

---

## 🔴 BLOCKER — Fix trước khi merge

### [B-001] Public setter trên Order aggregate
- **File**: `Orders/Domain/Entities/Order.cs:12`
- **Vi phạm**: `public OrderStatus Status { get; set; }`
- **Fix**: `{ get; private set; }`, thêm `MarkAsShipped()` domain method

---

## 🟠 HIGH

### [H-001] Domain method throws instead of returning Result
- **File**: `Disputes/Domain/Entities/Dispute.cs:45`
- **Fix**: Replace `throw new DomainException(...)` with `return Errors.Dispute.WindowClosed`

---

## 🟡 MEDIUM / 💡 SUGGESTION
...

---

## Invariant Coverage Map

| # | Invariant | Enforced In Domain? | Location |
|---|---|---|---|
| 1 | QuantityAvailable ≥ 0 | ✅ | InventoryItem.Reserve() |
| 2 | Order total immutable after CONFIRMED | ✅/❌ | Order.Create() |
| ... | ... | ... | ... |
```

---

## Phase 5: VERIFY

```bash
# Architecture tests — layer rules
dotnet test tests/MarketNest.ArchitectureTests --no-build -v minimal

# Unit tests — domain logic
dotnet test tests/MarketNest.UnitTests --no-build -v minimal

# Spot check: không còn public setter trên aggregates
find src/ -path "*/Domain/Entities/*.cs" -not -path "*/bin/*" \
  | xargs grep -n "{ get; set; }" | grep -v "//\|ValueObject\|private"
# Expect: 0 results

# Spot check: tất cả domain event implement IDomainEvent
find src/ -path "*/Domain/Events/*.cs" -not -path "*/bin/*" \
  | xargs grep -L "IDomainEvent" | grep -v "bin/"
# Expect: empty
```

---

## Quick Reference — DDD Rules cho MarketNest

| Pattern | Rule | Anti-pattern |
|---|---|---|
| Aggregate | `private set;` on all properties | `public set;` |
| Aggregate | `private Order() { }` constructor | `public Order() { }` |
| Aggregate | `static Result<T,Error> Create(...)` | `new Order(...)` in handler |
| Aggregate | Domain method raises event | Handler calls `AddDomainEvent()` |
| Aggregate | `IReadOnlyList<T>` for children | `List<T>` public |
| Aggregate | ID reference cross-aggregate | Nav prop cross-aggregate |
| Value Object | `record` or `: ValueObject` | Plain class with `Id` |
| Value Object | Validate in constructor | No validation |
| Value Object | No `Id` property | Has identity |
| Domain Event | Past tense `OrderPlacedEvent` | `PlaceOrderEvent` |
| Domain Event | Enough data for consumers | Minimal `(Guid id)` only |
| Domain Event | Implements `IDomainEvent` | Plain class |
| State Machine | `switch` expression | Nested if/else |
| State Machine | Return `Result<Unit, Error>` | `throw DomainException` |
| State Machine | Update timestamp on transition | No timestamp update |
| Result | Return `Result<T, Error>` from domain | `throw` business exception |
| Result | `var result = order.Ship(...); if (result.IsFailure)...` | Discard result |
| Errors | `"ORDER.INVALID_TRANSITION"` | `"Invalid"` |
| Invariant | Guard in domain method | Guard in Application handler |
