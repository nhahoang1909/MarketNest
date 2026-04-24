# MarketNest — Domain Design

> Version: 0.1 (Planning) | Status: Draft | Date: 2026-04  
> DDD Aggregates, Value Objects, Domain Events, Bounded Contexts

---

## 1. Bounded Contexts Map

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│                           MarketNest Platform                                    │
│                                                                                  │
│  ┌──────────────┐    ┌──────────────────────┐    ┌─────────────────────────┐   │
│  │   Identity   │    │       Catalog        │    │         Cart            │   │
│  │              │    │                      │    │                         │   │
│  │  User        │    │  Storefront          │    │  Cart                   │   │
│  │  Role        │    │  Product             │    │  CartItem               │   │
│  │  RefreshToken│    │  ProductVariant      │    │  (Redis reservations)   │   │
│  └──────────────┘    │  InventoryItem       │    └──────────┬──────────────┘   │
│         │            └──────────┬───────────┘               │                  │
│         │                       │                    checkout│                  │
│         │            ┌──────────▼───────────┐               │                  │
│         │            │        Orders        │◄──────────────┘                  │
│         │            │                      │                                   │
│         │            │  Order               │                                   │
│         │            │  OrderLine           │                                   │
│         │            │  Fulfillment         │                                   │
│         │            │  Shipment            │                                   │
│         │            └──────────┬───────────┘                                   │
│         │                       │                                               │
│         │          ┌────────────┼────────────┐                                 │
│         │          │            │            │                                  │
│         │  ┌───────▼──┐  ┌─────▼─────┐  ┌──▼──────────┐                      │
│         │  │ Payments │  │  Reviews  │  │  Disputes   │                      │
│         │  │          │  │           │  │             │                      │
│         │  │ Payment  │  │ Review    │  │ Dispute     │                      │
│         │  │ Payout   │  │ ReviewVote│  │ Message     │                      │
│         │  │ Commission│ └───────────┘  │ Resolution  │                      │
│         │  └──────────┘                 └─────────────┘                      │
│         │                                                                      │
│         └────────────────────────► Notifications                              │
│                                    (cross-cutting, Phase 3 = own service)      │
└──────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Aggregate Designs

### 2.1 Storefront Aggregate

```
Storefront (Aggregate Root)
├── StoreId: Guid
├── SellerId: Guid (FK to Identity, not nav prop)
├── Slug: StorefrontSlug (value object — URL-safe, immutable after activation)
├── Name: string
├── Description: string
├── BannerImageUrl: string?
├── Status: StorefrontStatus { Draft | Active | Suspended | Closed }
├── CommissionRate: decimal (platform fee %, set by admin)
├── ActivatedAt: DateTime?
├── Products: List<ProductId> (IDs only — Product is own aggregate)
└── Terms: SellerTermsAcceptance (value object)

Domain Events:
  StorefrontActivatedEvent
  StorefrontSuspendedEvent(reason)
  StorefrontClosedEvent

Business Rules:
  - Slug is immutable after StorefrontStatus becomes Active
  - Storefront cannot be activated without email verification
  - Only Admin can change CommissionRate
```

### 2.2 Product Aggregate

```
Product (Aggregate Root)
├── ProductId: Guid
├── StoreId: Guid (FK to Storefront)
├── Title: string (max 200)
├── Description: string (max 5000)
├── Category: ProductCategory (value object — code + display name)
├── Tags: List<string> (max 10 tags)
├── Status: ProductStatus { Draft | Active | Archived }
├── CreatedAt / UpdatedAt
├── IsDeleted: bool (soft delete)
│
├── Variants: List<ProductVariant> (1..*)
│   └── ProductVariant
│       ├── VariantId: Guid
│       ├── ProductId: Guid
│       ├── Sku: Sku (value object — platform-unique string)
│       ├── Attributes: Dictionary<string, string> (e.g. { "Size": "M", "Color": "Red" })
│       ├── Price: Money (value object — amount + currency)
│       ├── CompareAtPrice: Money? (must be > Price if set)
│       ├── Status: VariantStatus { Active | Inactive }
│       └── InventoryItem: InventoryItem (1:1)
│           ├── InventoryItemId: Guid
│           ├── VariantId: Guid
│           ├── QuantityOnHand: int (≥ 0)
│           ├── QuantityReserved: int (≥ 0)
│           └── QuantityAvailable: int (computed: OnHand - Reserved)

Domain Events:
  ProductPublishedEvent
  ProductArchivedEvent
  InventoryLowEvent (when QuantityAvailable < 5)
  InventoryDepletedEvent (when QuantityAvailable = 0)

Business Rules:
  - Product cannot be published (Active) without at least 1 Active variant
  - QuantityOnHand + QuantityReserved constraints enforced at DB level
  - CompareAtPrice must be strictly > Price
  - Sku must be unique across the platform (enforced by unique DB index)
```

### 2.3 Cart Aggregate

```
Cart (Aggregate Root)
├── CartId: Guid
├── BuyerId: Guid (FK to Identity)
├── Status: CartStatus { Active | CheckedOut | Abandoned }
├── CreatedAt / UpdatedAt
│
└── Items: List<CartItem>
    └── CartItem
        ├── CartItemId: Guid
        ├── CartId: Guid
        ├── VariantId: Guid
        ├── ProductTitle: string (snapshot)
        ├── VariantAttributes: Dictionary<string,string> (snapshot)
        ├── SnapshotPrice: Money (price at time of add)
        ├── CurrentPrice: Money (live price — fetched, not stored; shown for comparison)
        ├── Quantity: int (1–99)
        └── AddedAt: DateTime

Domain Events:
  CartItemAddedEvent(variantId, qty)
  CartItemRemovedEvent(variantId)
  CartCheckedOutEvent(cartId, buyerId)
  CartAbandonedEvent (after 7 days inactive)

Business Rules:
  - Max 20 distinct items per cart
  - Adding same variantId increases quantity (merge)
  - Cannot add to CheckedOut cart
  - Price drift > 5% triggers warning (business rule, not invariant)
  - Reservation managed externally (Redis TTL) — Cart does not own reservation state
```

### 2.4 Order Aggregate

```
Order (Aggregate Root)
├── OrderId: Guid
├── BuyerId: Guid
├── Status: OrderStatus { Pending | Confirmed | Processing | Shipped | Delivered | Completed | Cancelled | Disputed | Refunded }
├── ShippingAddress: Address (value object — snapshot at order time)
├── PlacedAt: DateTime
├── ConfirmedAt / ShippedAt / DeliveredAt / CompletedAt / CancelledAt: DateTime?
├── CancellationReason: string?
├── Notes: string?
│
├── Lines: List<OrderLine> (1..*)
│   └── OrderLine
│       ├── OrderLineId: Guid
│       ├── OrderId: Guid
│       ├── VariantId: Guid
│       ├── ProductTitle: string (snapshot)
│       ├── VariantAttributes: Dictionary<string,string> (snapshot)
│       ├── UnitPrice: Money (snapshot — immutable after Confirmed)
│       ├── Quantity: int
│       ├── LineTotal: Money (computed: UnitPrice × Quantity)
│       └── StoreId: Guid (for grouping into Fulfillments)
│
└── Fulfillments: List<Fulfillment> (1 per seller in the order)
    └── Fulfillment
        ├── FulfillmentId: Guid
        ├── OrderId: Guid
        ├── StoreId: Guid
        ├── Status: FulfillmentStatus (mirrors OrderStatus subset)
        ├── TrackingNumber: string?
        ├── TrackingUrl: string?
        ├── ShippedAt: DateTime?
        └── Lines: List<OrderLineId> (references)

Derived Values (computed, not stored):
  Subtotal = sum of all LineTotals
  Total = Subtotal + ShippingFee - Discount

Domain Events:
  OrderPlacedEvent(orderId, buyerId, lines[], total)
  OrderConfirmedEvent(orderId, sellerId)
  OrderShippedEvent(orderId, trackingNumber)
  OrderDeliveredEvent(orderId)
  OrderCompletedEvent(orderId, sellerId, payoutAmount)
  OrderCancelledEvent(orderId, reason, refundAmount)
  OrderDisputedEvent(orderId, buyerId)
  OrderRefundedEvent(orderId, refundAmount)
```

### 2.5 Payment Aggregate

```
Payment (Aggregate Root)
├── PaymentId: Guid
├── OrderId: Guid
├── BuyerId: Guid
├── Amount: Money
├── Method: PaymentMethod { CreditCard | BankTransfer } (stub Phase 1)
├── Status: PaymentStatus { Pending | Captured | Refunded | Failed }
├── GatewayReference: string? (external payment gateway ID)
├── CapturedAt: DateTime?
├── FailureReason: string?
│
├── Commission: Commission
│   ├── CommissionId: Guid
│   ├── PaymentId: Guid
│   ├── RateSnapshot: decimal (rate at time of order — MUST snapshot)
│   ├── CommissionAmount: Money
│   └── CalculatedAt: DateTime
│
└── Payout: Payout?
    ├── PayoutId: Guid
    ├── PaymentId: Guid
    ├── SellerId: Guid
    ├── GrossAmount: Money (order subtotal)
    ├── CommissionDeducted: Money
    ├── ProcessingFeeDeducted: Money
    ├── NetAmount: Money (actual seller receives)
    ├── Status: PayoutStatus { Pending | Processing | Paid | Failed | Clawback }
    ├── ScheduledFor: DateTime
    └── ProcessedAt: DateTime?

Domain Events:
  PaymentCapturedEvent(paymentId, orderId, amount)
  PaymentFailedEvent(paymentId, reason)
  PaymentRefundedEvent(paymentId, refundAmount)
  PayoutScheduledEvent(payoutId, sellerId, netAmount, scheduledFor)
  PayoutProcessedEvent(payoutId, sellerId, netAmount)
  PayoutClawbackRequiredEvent(payoutId, reason)
```

### 2.6 Review Aggregate

```
Review (Aggregate Root)
├── ReviewId: Guid
├── ProductId: Guid
├── OrderId: Guid (gate: must be COMPLETED order for this product)
├── BuyerId: Guid
├── Rating: Rating (value object — int 1..5)
├── Title: string? (max 100)
├── Body: string? (max 1000)
├── Status: ReviewStatus { Published | Hidden | Flagged }
├── SubmittedAt: DateTime
├── IsEditable: bool (true for 24h after submission)
├── SellerReply: SellerReply? (entity)
│   ├── Body: string (max 500)
│   └── RepliedAt: DateTime
│
└── Votes: List<ReviewVote>
    └── ReviewVote
        ├── ReviewId: Guid
        ├── VoterId: Guid (BuyerId)
        └── VotedAt: DateTime

Domain Events:
  ReviewSubmittedEvent(reviewId, productId, storeId, rating)
  ReviewHiddenEvent(reviewId, reason)
  SellerReplyAddedEvent(reviewId, storeId)

Business Rules:
  - ReviewGate: buyer must have COMPLETED order containing this productId
  - One review per buyer per product-order combination
  - Reviews immutable after 24h
  - Seller can reply once (cannot edit reply)
  - ReviewVote: one vote per buyer per review
```

### 2.7 Dispute Aggregate

```
Dispute (Aggregate Root)
├── DisputeId: Guid
├── OrderId: Guid (1:1 — one dispute per order)
├── BuyerId: Guid
├── SellerId: Guid (denormalized from order)
├── Status: DisputeStatus { Open | AwaitingSellerResponse | UnderReview | Resolved }
├── Reason: DisputeReason { NotReceived | NotAsDescribed | Damaged | WrongItem | Other }
├── OpenedAt: DateTime
├── SellerResponseDeadline: DateTime (OpenedAt + 72h)
├── ResolvedAt: DateTime?
│
├── Messages: List<DisputeMessage> (immutable once added)
│   └── DisputeMessage
│       ├── MessageId: Guid
│       ├── DisputeId: Guid
│       ├── AuthorId: Guid
│       ├── AuthorRole: DisputeParty { Buyer | Seller | Admin }
│       ├── Body: string (max 2000)
│       ├── EvidenceUrls: List<string> (max 5 images)
│       └── SentAt: DateTime
│
└── Resolution: Resolution?
    ├── ResolutionId: Guid
    ├── DisputeId: Guid
    ├── AdminId: Guid
    ├── Decision: ResolutionDecision { FullRefund | PartialRefund | DismissBuyerClaim }
    ├── RefundAmount: Money?
    ├── AdminNote: string
    └── ResolvedAt: DateTime

Domain Events:
  DisputeOpenedEvent(disputeId, orderId, buyerId, sellerId)
  DisputeSellerRespondedEvent(disputeId)
  DisputeEscalatedEvent(disputeId) -- seller no-response after deadline
  DisputeResolvedEvent(disputeId, decision, refundAmount?)

Business Rules:
  - Dispute can only be opened within 3 days of order DELIVERED
  - Only one open dispute per order
  - Messages are immutable
  - Seller must respond within 72h or case auto-escalates
```

---

## 3. Value Objects

```csharp
// Money — used across all financial aggregates
public record Money(decimal Amount, string Currency = "USD")
{
    public static Money Zero => new(0m, "USD");
    public static Money Of(decimal amount) => amount >= 0
        ? new Money(amount)
        : throw new DomainException("Amount cannot be negative");
    
    public Money Add(Money other) => Currency == other.Currency
        ? new Money(Amount + other.Amount, Currency)
        : throw new DomainException("Cannot add different currencies");
    
    public Money MultiplyBy(int factor) => new Money(Amount * factor, Currency);
    public Money Percentage(decimal rate) => new Money(Math.Round(Amount * rate, 2), Currency);
    
    public string Formatted => Amount.ToString("C2", CultureInfo.GetCultureInfo("en-US"));
}

// StorefrontSlug — URL-safe, immutable
public record StorefrontSlug
{
    public string Value { get; }
    public StorefrontSlug(string value)
    {
        if (!Regex.IsMatch(value, @"^[a-z0-9-]{3,50}$"))
            throw new DomainException("Slug must be 3-50 lowercase alphanumeric characters and hyphens");
        Value = value;
    }
    public static implicit operator string(StorefrontSlug slug) => slug.Value;
}

// Address — shipping address snapshot
public record Address(
    string RecipientName,
    string Street,
    string? Street2,
    string City,
    string StateProvince,
    string PostalCode,
    string CountryCode)  // ISO 3166-1 alpha-2
{
    public string OneLine => $"{Street}, {City}, {StateProvince} {PostalCode}, {CountryCode}";
}

// Rating — 1..5 stars
public record Rating
{
    public int Value { get; }
    public Rating(int value)
    {
        if (value is < 1 or > 5)
            throw new DomainException("Rating must be between 1 and 5");
        Value = value;
    }
}

// Sku — platform-unique product variant code
public record Sku
{
    public string Value { get; }
    public Sku(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 50)
            throw new DomainException("SKU must be 1-50 characters");
        Value = value.ToUpperInvariant();
    }
}
```

---

## 4. Domain Events Summary

| Event | Raised By | Handled By |
|-------|-----------|-----------|
| `StorefrontActivatedEvent` | Storefront | Notifications |
| `ProductPublishedEvent` | Product | (future: search index) |
| `InventoryLowEvent` | InventoryItem | Notifications → Seller |
| `CartCheckedOutEvent` | Cart | Orders (create order) |
| `OrderPlacedEvent` | Order | Notifications (buyer + seller), Payments |
| `OrderConfirmedEvent` | Order | Notifications (buyer) |
| `OrderShippedEvent` | Order | Notifications (buyer) |
| `OrderDeliveredEvent` | Order | Notifications (buyer), start dispute window |
| `OrderCompletedEvent` | Order | Payments (schedule payout) |
| `OrderCancelledEvent` | Order | Payments (trigger refund), Inventory (release) |
| `PaymentCapturedEvent` | Payment | Orders (advance to Confirmed) |
| `PaymentRefundedEvent` | Payment | Notifications (buyer) |
| `PayoutProcessedEvent` | Payout | Notifications (seller) |
| `ReviewSubmittedEvent` | Review | Catalog (recalculate rating), Notifications (seller) |
| `DisputeOpenedEvent` | Dispute | Orders (set DISPUTED), Notifications (seller + admin) |
| `DisputeEscalatedEvent` | Dispute | Notifications (admin) |
| `DisputeResolvedEvent` | Dispute | Orders (set COMPLETED or REFUNDED), Payments |

---

## 5. Invariants Table

| # | Invariant | Enforced In |
|---|-----------|-------------|
| 1 | QuantityAvailable ≥ 0 | DB check constraint + Application |
| 2 | Order total immutable after CONFIRMED | Domain method guard |
| 3 | Payout only for COMPLETED order | Application + Payments domain |
| 4 | Review only by buyer of COMPLETED order | ReviewGate service |
| 5 | Dispute only within 3 days of DELIVERED | Domain method guard |
| 6 | Commission rate snapshotted at order time | Commission entity |
| 7 | Storefront slug immutable after activation | StorefrontSlug VO + Domain guard |
| 8 | Price of OrderLine immutable after CONFIRMED | OrderLine private setters |
| 9 | Max 1 open dispute per Order | DB unique constraint + Domain guard |
| 10 | Seller cannot modify order prices after CONFIRMED | Order state guard |
