# MarketNest — Domain Design & Business Rules

> Version: 0.2 (Planning) | Status: Draft | Date: 2026-04
> Consolidated from: `domain-design.md` + `business-logic-requirements.md`

---

## Table of Contents

1. [Bounded Contexts Map](#1-bounded-contexts-map)
2. [Actors](#2-actors)
3. [Aggregate Designs](#3-aggregate-designs)
4. [Value Objects](#4-value-objects)
5. [Business Rules by Domain](#5-business-rules-by-domain)
6. [Domain Events Summary](#6-domain-events-summary)
7. [Invariants Table](#7-invariants-table)
8. [Notification Triggers](#8-notification-triggers)
9. [User Settings & Preferences](#9-user-settings--preferences)

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
│         │            │  Order, OrderLine    │                                   │
│         │            │  Fulfillment         │                                   │
│         │            └──────────┬───────────┘                                   │
│         │          ┌────────────┼────────────┐                                 │
│         │  ┌───────▼──┐  ┌─────▼─────┐  ┌──▼──────────┐                      │
│         │  │ Payments │  │  Reviews  │  │  Disputes   │                      │
│         │  │ Payment  │  │ Review    │  │ Dispute     │                      │
│         │  │ Payout   │  │ ReviewVote│  │ Message     │                      │
│         │  │ Commission│ └───────────┘  │ Resolution  │                      │
│         │  └──────────┘                 └─────────────┘                      │
│         └────────────────────────► Notifications (cross-cutting)               │
└──────────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Actors

| Actor | Description | Permissions |
|-------|-------------|-------------|
| **Guest** | Unauthenticated visitor | Browse catalog, view storefronts |
| **Buyer** | Registered customer | Place orders, write reviews (after purchase), open disputes |
| **Seller** | Merchant with storefront | Manage products/inventory, fulfill orders, respond to disputes |
| **Admin** | Platform operator | Arbitrate disputes, manage commissions, ban users |

---

## 3. Aggregate Designs

### 3.1 Storefront Aggregate

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
  StorefrontActivatedEvent, StorefrontSuspendedEvent(reason), StorefrontClosedEvent
```

**Business Rules:**
- Each Seller has exactly **one** Storefront
- Slug is immutable after StorefrontStatus becomes Active
- Storefront cannot be activated without email verification
- Only Admin can change CommissionRate
- A Suspended storefront hides all products from public view

### 3.2 Product Aggregate

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
│       ├── Sku: Sku (value object — platform-unique)
│       ├── Attributes: Dictionary<string, string> (e.g. { "Size": "M", "Color": "Red" })
│       ├── Price: Money (value object)
│       ├── CompareAtPrice: Money? (must be > Price if set)
│       ├── Status: VariantStatus { Active | Inactive }
│       └── InventoryItem: InventoryItem (1:1)
│           ├── QuantityOnHand: int (≥ 0)
│           ├── QuantityReserved: int (≥ 0)
│           └── QuantityAvailable: int (computed: OnHand - Reserved)

Domain Events:
  ProductPublishedEvent, ProductArchivedEvent
  InventoryLowEvent (QuantityAvailable < 5), InventoryDepletedEvent (= 0)
```

**Business Rules:**
- Product belongs to exactly one Storefront
- Only Active products appear in search/browse
- Cannot be published without at least 1 Active variant
- Price must be > 0; CompareAtPrice must be strictly > Price
- Sku must be unique across the platform (unique DB index)
- Inventory can never go negative: guard at DB level (check constraint) AND application level

### 3.3 Cart Aggregate

```
Cart (Aggregate Root)
├── CartId: Guid
├── BuyerId: Guid
├── Status: CartStatus { Active | CheckedOut | Abandoned }
│
└── Items: List<CartItem>
    └── CartItem
        ├── VariantId: Guid
        ├── ProductTitle: string (snapshot)
        ├── VariantAttributes: Dictionary<string,string> (snapshot)
        ├── SnapshotPrice: Money (price at time of add)
        ├── CurrentPrice: Money (live, fetched not stored)
        ├── Quantity: int (1–99)
        └── AddedAt: DateTime

Domain Events:
  CartItemAddedEvent, CartItemRemovedEvent, CartCheckedOutEvent, CartAbandonedEvent
```

**Business Rules:**
- Each authenticated Buyer has **one** active Cart
- Guest carts are session-local (not persisted to DB)
- On login, guest cart merges into user's cart (quantity union, max = stock)
- Max 20 distinct items per cart; max 99 quantity per item
- Adding same variantId increases quantity (merge)
- Cannot add to CheckedOut cart
- Price drift > 5% triggers warning
- Reservation managed externally (Redis TTL)

### Inventory Reservation (TTL via Redis)

```
Add to Cart:
  1. Check quantityAvailable ≥ requested qty
  2. Atomically increment quantityReserved in DB (pessimistic lock)
  3. Set Redis key: marketnest:cart:{userId}:reservation:{variantId} = qty, TTL=15min
  4. Each cart page view / heartbeat refreshes TTL (EXPIRE reset)

Remove from Cart / TTL Expiry:
  1. Decrement quantityReserved in DB
  2. Delete Redis key

TTL Expiry Handling:
  - Redis keyspace notification → background service → releases DB reservation
  - Scheduled cleanup job every 5min releases reservations older than 20min
```

### 3.4 Order Aggregate

```
Order (Aggregate Root)
├── OrderId: Guid
├── BuyerId: Guid
├── Status: OrderStatus { Pending | Confirmed | Processing | Shipped | Delivered | Completed | Cancelled | Disputed | Refunded }
├── ShippingAddress: Address (snapshot at order time)
├── PlacedAt, ConfirmedAt, ShippedAt, DeliveredAt, CompletedAt, CancelledAt: DateTime?
├── CancellationReason: string?
│
├── Lines: List<OrderLine> (1..*)
│   └── OrderLine
│       ├── VariantId, ProductTitle, VariantAttributes (snapshots)
│       ├── UnitPrice: Money (immutable after Confirmed)
│       ├── Quantity: int
│       ├── LineTotal: Money (computed)
│       └── StoreId: Guid
│
└── Fulfillments: List<Fulfillment> (1 per seller)
    └── Fulfillment
        ├── StoreId, Status, TrackingNumber, TrackingUrl, ShippedAt

Derived: Subtotal = sum LineTotals; Total = Subtotal + ShippingFee - Discount

Domain Events:
  OrderPlacedEvent, OrderConfirmedEvent, OrderShippedEvent, OrderDeliveredEvent,
  OrderCompletedEvent, OrderCancelledEvent, OrderDisputedEvent, OrderRefundedEvent
```

#### Order State Machine

```
                          ┌──────────────┐
                          │   PENDING    │ ← Created on checkout
                          └──────┬───────┘
                                 │ Payment confirmed
                          ┌──────▼───────┐
                          │  CONFIRMED   │ ← Seller must act within 48h
                          └──────┬───────┘
                     ┌───────────┴──────────┐
              Seller confirms            Seller cancels
            ┌────────▼───────┐   ┌────────▼───────┐
            │   PROCESSING   │   │   CANCELLED    │ ← Refund
            └────────┬───────┘   └────────────────┘
                     │ Seller ships
            ┌────────▼───────┐
            │    SHIPPED     │ ← Tracking required
            └────────┬───────┘
                     │ Buyer confirms (or auto after 7d)
            ┌────────▼───────┐
            │   DELIVERED    │
            └────┬───────────┘
       ┌─────────┴──────────┐
  No dispute              Dispute within 3 days
┌──────▼──────┐         ┌───────▼──────┐
│  COMPLETED  │         │   DISPUTED   │
└─────────────┘         └───────┬──────┘
                        Admin arbitrates
                   ┌──────┴──────┐
             Buyer wins        Seller wins
              │                │
       ┌──────▼──────┐  ┌──────▼──────┐
       │  REFUNDED   │  │  COMPLETED  │
       └─────────────┘  └─────────────┘
```

**Auto-Actions (Background Jobs):**

| Trigger | Action |
|---------|--------|
| Seller no action within 48h of CONFIRMED | Auto-CANCELLED + Buyer refunded |
| SHIPPED for 30 days, no confirmation | Auto-DELIVERED → auto-COMPLETED |
| DELIVERED for 3 days, no dispute | Auto-COMPLETED |
| COMPLETED order | Trigger payout calculation |

### 3.5 Payment Aggregate

```
Payment (Aggregate Root)
├── PaymentId: Guid
├── OrderId, BuyerId: Guid
├── Amount: Money
├── Method: PaymentMethod { CreditCard | BankTransfer } (stub Phase 1)
├── Status: PaymentStatus { Pending | Captured | Refunded | Failed }
├── GatewayReference: string?
│
├── Commission
│   ├── RateSnapshot: decimal (rate at order time — MUST snapshot)
│   ├── CommissionAmount: Money
│
└── Payout?
    ├── SellerId: Guid
    ├── GrossAmount, CommissionDeducted, ProcessingFeeDeducted, NetAmount: Money
    ├── Status: PayoutStatus { Pending | Processing | Paid | Failed | Clawback }
    ├── ScheduledFor, ProcessedAt: DateTime?

Domain Events:
  PaymentCapturedEvent, PaymentFailedEvent, PaymentRefundedEvent,
  PayoutScheduledEvent, PayoutProcessedEvent, PayoutClawbackRequiredEvent
```

**Commission Engine:**
```
Platform Commission = Order Subtotal × Commission Rate (default: 10%)
Seller Payout = Order Subtotal - Commission - Processing Fee (2.9% + $0.30 stub)
Payout batch runs daily at 02:00 UTC
```

**Refund Rules:**
- Full refund on CANCELLED orders: immediately
- Partial refund after dispute: Admin-specified amount
- Refunds reverse payout if already disbursed (Clawback)

### 3.6 Review Aggregate

```
Review (Aggregate Root)
├── ReviewId: Guid
├── ProductId, OrderId, BuyerId: Guid
├── Rating: Rating (value object — int 1..5)
├── Title: string? (max 100), Body: string? (max 1000)
├── Status: ReviewStatus { Published | Hidden | Flagged }
├── IsEditable: bool (true for 24h)
├── SellerReply: SellerReply? (Body max 500, RepliedAt)
└── Votes: List<ReviewVote> (VoterId, VotedAt)

Domain Events:
  ReviewSubmittedEvent, ReviewHiddenEvent, SellerReplyAddedEvent
```

**Review Gate (Anti-Fraud):**
- Must be authenticated Buyer
- Must have Order containing this Product in COMPLETED state
- Has NOT already reviewed for this Order
- NOT if Order in DISPUTED/REFUNDED state (configurable)

**Rules:** Reviews immutable after 24h. Seller replies once. One vote per buyer per review.

**Aggregation:** Product rating = avg of published ratings. Storefront rating = weighted average. Recalculated event-driven.

### 3.7 Dispute Aggregate

```
Dispute (Aggregate Root)
├── DisputeId: Guid
├── OrderId (1:1), BuyerId, SellerId: Guid
├── Status: DisputeStatus { Open | AwaitingSellerResponse | UnderReview | Resolved }
├── Reason: DisputeReason { NotReceived | NotAsDescribed | Damaged | WrongItem | Other }
├── SellerResponseDeadline: DateTime (OpenedAt + 72h)
│
├── Messages: List<DisputeMessage> (immutable)
│   └── AuthorRole: { Buyer | Seller | Admin }, Body, EvidenceUrls (max 5)
│
└── Resolution?
    ├── Decision: { FullRefund | PartialRefund | DismissBuyerClaim }
    ├── RefundAmount: Money?, AdminNote: string

Domain Events:
  DisputeOpenedEvent, DisputeSellerRespondedEvent,
  DisputeEscalatedEvent, DisputeResolvedEvent
```

**Rules:**
- Only within 3 days of DELIVERED; one dispute per order
- Seller has 72h to respond or auto-escalates
- Both parties can submit evidence (text + up to 5 photos)
- All messages immutable (audit trail)

---

## 4. Value Objects

> **Convention (ADR-007):** Class-based VOs use `{ get; }`. Record-based VOs use `{ get; init; }` or `{ get; }` for validated VOs.

```csharp
// Money — financial operations
public record Money(decimal Amount, string Currency = "USD")
{
    public static Money Zero => new(0m, "USD");
    public static Money Of(decimal amount) => amount >= 0
        ? new Money(amount)
        : throw new DomainException("Amount cannot be negative");
    public Money Add(Money other) => Currency == other.Currency
        ? new Money(Amount + other.Amount, Currency)
        : throw new DomainException("Cannot add different currencies");
    public Money MultiplyBy(int factor) => new(Amount * factor, Currency);
    public Money Percentage(decimal rate) => new(Math.Round(Amount * rate, 2), Currency);
}

// StorefrontSlug — URL-safe, immutable
public record StorefrontSlug
{
    public string Value { get; }
    public StorefrontSlug(string value)
    {
        if (!Regex.IsMatch(value, @"^[a-z0-9-]{3,50}$"))
            throw new DomainException("Slug must be 3-50 lowercase alphanumeric/hyphens");
        Value = value;
    }
}

// Address — shipping address snapshot
public record Address(string RecipientName, string Street, string? Street2,
    string City, string StateProvince, string PostalCode, string CountryCode);

// Rating — 1..5 stars
public record Rating { public int Value { get; } /* validated 1-5 */ }

// Sku — platform-unique product variant code
public record Sku { public string Value { get; } /* validated, uppercased, max 50 */ }
```

---

## 5. Business Rules by Domain

### 5.1 Platform Rules (Admin)

- Seller must verify email before activating Storefront
- Seller must agree to Terms of Service (checkbox + timestamp)
- Prohibited categories: configurable list
- Admin can suspend any Storefront or Product with reason
- Commission rate configurable per Seller; default 10%
- Rate changes take effect on Orders created after change date

---

## 6. Domain Events Summary

| Event | Raised By | Handled By |
|-------|-----------|-----------|
| `StorefrontActivatedEvent` | Storefront | Notifications |
| `ProductPublishedEvent` | Product | (future: search index) |
| `InventoryLowEvent` | InventoryItem | Notifications → Seller |
| `CartCheckedOutEvent` | Cart | Orders (create order) |
| `OrderPlacedEvent` | Order | Notifications (buyer + seller), Payments |
| `OrderConfirmedEvent` | Order | Notifications (buyer) |
| `OrderShippedEvent` | Order | Notifications (buyer) |
| `OrderDeliveredEvent` | Order | Notifications, start dispute window |
| `OrderCompletedEvent` | Order | Payments (schedule payout) |
| `OrderCancelledEvent` | Order | Payments (refund), Inventory (release) |
| `PaymentCapturedEvent` | Payment | Orders (advance to Confirmed) |
| `PaymentRefundedEvent` | Payment | Notifications (buyer) |
| `PayoutProcessedEvent` | Payout | Notifications (seller) |
| `ReviewSubmittedEvent` | Review | Catalog (recalculate rating), Notifications |
| `DisputeOpenedEvent` | Dispute | Orders (set DISPUTED), Notifications |
| `DisputeEscalatedEvent` | Dispute | Notifications (admin) |
| `DisputeResolvedEvent` | Dispute | Orders, Payments |

---

## 7. Invariants Table

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

---

## 8. Notification Triggers

> All notifications below are subject to user's `NotificationPreference` toggles and frequency settings (§9.5).
> The Notifications module checks `INotificationPreferenceReadService` before dispatching.

| Event | Recipients | Channel | Preference Toggle |
|-------|-----------|---------|-------------------|
| Order placed | Buyer + Seller | Email | `NotifyOrderPlaced` |
| Order confirmed by Seller | Buyer | Email | `NotifyOrderConfirmed` |
| Order shipped (tracking added) | Buyer | Email | `NotifyOrderShipped` |
| Order auto-confirmed after 7 days | Buyer | Email | `NotifyOrderDelivered` |
| Dispute opened | Seller + Admin | Email | `NotifyDisputeOpened` |
| Seller responds to dispute | Buyer + Admin | Email | `NotifyDisputeResolved` |
| Admin resolves dispute | Buyer + Seller | Email | `NotifyDisputeResolved` |
| Payout processed | Seller | Email | `NotifyPaymentProcessed` |
| Review left on storefront | Seller | Email (digest, daily) | `NotifyReviewReceived` |
| Password reset requested | User | Email | Always sent (security) |
| New login from unknown device | User | Email | Always sent (security) |

---

## 9. User Settings & Preferences

> Added 2026-04-25. Spec version 1.0. Phase 1 foundations + Phase 2 extensions.
> Key principle: **settings are distributed across owning modules** per module boundary rules (ADR-004).
> Identity owns user profile and core preferences. Other modules own their domain-specific preferences.

### 9.1 Module Ownership Map

| Setting Entity | Owner Module | Schema | Rationale |
|----------------|-------------|--------|-----------|
| User (profile fields) | Identity | `identity` | Core user identity data |
| UserAddress | Identity | `identity` | User's personal address book |
| UserPreferences | Identity | `identity` | Timezone, date/time format, currency display, language |
| NotificationPreference | Identity | `identity` | Email toggles, frequency, alternate email — owned by user identity |
| UserPrivacy | Identity | `identity` | Profile visibility, search visibility, GDPR consent |
| UserFavoriteSeller | Catalog | `catalog` | Follows storefronts — Catalog owns storefront relationships |
| WishlistItem | Cart | (Redis or `cart` schema) | Saved products for future purchase — Cart-adjacent |
| UserShippingPreference | Orders | `orders` | Shipping speed, cost tolerance — affects checkout/orders |
| OrderPreference | Orders | `orders` | Order notification delay, dispute resolution preference |
| UserSession | Identity | `identity` | Active sessions, device tracking (Phase 2) |
| UserTwoFactorAuth | Identity | `identity` | 2FA TOTP setup (Phase 2) |
| PaymentMethod | Payments | `payments` | Saved payment methods (Phase 2+) |

### 9.2 Identity Module — User Profile Extensions

```
User (existing Aggregate Root — extend)
├── ...existing fields...
├── PhoneNumber: string? (E.164 format, optional)
├── AvatarFileId: Guid? (FK to UploadedFile, not raw URL)
├── PublicBio: string? (max 500, seller only)
├── EmailVerified: bool
```

**Business Rules:**
- Email is read-only from profile page (change email flow is a separate auth operation)
- AvatarFileId references `UploadedFile` entity (per file upload pipeline, §7 in backend-infrastructure)
- PublicBio is only editable by users with Seller role
- PhoneNumber validated as E.164 format (optional)

### 9.3 UserAddress Entity (Identity)

```
UserAddress (Entity, owned by User)
├── Id: Guid
├── UserId: Guid
├── RecipientName: string (max 100)
├── Street: string (max 200)
├── Street2: string? (max 200)
├── City: string (max 100)
├── StateProvince: string (max 100)
├── PostalCode: string (max 20)
├── CountryCode: string (ISO 3166-1 alpha-2, 2 chars)
├── Label: AddressLabel { Home | Office | Other }
├── IsDefault: bool
├── CreatedAt: DateTime
├── UpdatedAt: DateTime?
```

**Business Rules:**
- Max **10** addresses per user
- Exactly **1** default address at a time (enforced via domain method + unique partial index)
- Cannot delete the default address unless another address is set as default first
- Cannot delete the only remaining address (if it's used in any active order)
- CountryCode validated against ISO 3166-1 alpha-2

**Invariants:**
| # | Invariant | Enforced In |
|---|-----------|-------------|
| 11 | Max 10 addresses per user | Application (command handler) + DB check |
| 12 | Exactly 1 default address | Domain method + unique partial index (`WHERE is_default = true`) |

### 9.4 UserPreferences Entity (Identity)

```
UserPreferences (Entity, 1:1 with User)
├── Id: Guid
├── UserId: Guid
├── Timezone: string (IANA timezone ID, default "UTC")
├── TimeFormat: TimeFormatPreference { TwelveHour | TwentyFourHour }
├── DateFormat: DateFormatPreference { MonthDayYear | DayMonthYear }
├── CurrencyDisplay: string (ISO 4217, default "USD", display-only)
├── Language: string (BCP 47, default "en") — Phase 2: wired to localization
├── UpdatedAt: DateTime
```

**Business Rules:**
- Timezone must be a valid IANA timezone ID (validated via `TimeZoneInfo.FindSystemTimeZoneById`)
- CurrencyDisplay is for display formatting only — does NOT affect checkout currency
- Language defaults to "en"; Phase 2 wires to ASP.NET Core localization cookie
- All timestamps in DB remain UTC; conversion happens at display time via `IUserTimeZoneProvider`
- Created automatically with defaults when user registers (seeded via domain event or lazy creation)

**Integration with `IUserTimeZoneProvider`:**
- `HttpContextUserTimeZoneProvider` reads from `UserPreferences` (DB-backed, cached per request)
- Falls back to cookie → UTC if no preferences exist yet
- All Razor views use `IUserTimeZoneProvider` for date/time display

### 9.5 NotificationPreference Entity (Identity)

```
NotificationPreference (Entity, 1:1 with User)
├── Id: Guid
├── UserId: Guid
│
├── AlternateEmail: string? (optional, verified before use)
├── AlternateEmailVerified: bool
│
├── NotifyOrderPlaced: bool (default true)
├── NotifyOrderConfirmed: bool (default true)
├── NotifyOrderShipped: bool (default true)
├── NotifyOrderDelivered: bool (default true)
├── NotifyDisputeOpened: bool (default true)
├── NotifyDisputeResolved: bool (default true)
├── NotifyReviewReceived: bool (default true, seller-only)
├── NotifyPaymentProcessed: bool (default true, seller-only)
├── SubscribeNewsletter: bool (default false)
│
├── Frequency: NotificationFrequency { RealTime | OneHourDigest | DailyDigest }
├── NotificationTarget: NotificationTarget { Primary | Alternate | Both }
│
├── UpdatedAt: DateTime
```

**Business Rules:**
- AlternateEmail requires verification before it can be used as notification target
- NotificationTarget cannot include "Alternate" or "Both" unless AlternateEmailVerified is true
- Seller-specific toggles (NotifyReviewReceived, NotifyPaymentProcessed) only shown to Seller role users
- DailyDigest sends at 9:00 AM user's timezone (requires UserPreferences.Timezone)
- Created with sensible defaults when user registers

**Phase 1 scope:** All toggles + frequency selection. Alternate email: Phase 1 (UI + verification). Digest batching background job: Phase 1 (simple implementation).

### 9.6 UserPrivacy Entity (Identity)

```
UserPrivacy (Entity, 1:1 with User)
├── Id: Guid
├── UserId: Guid
├── ProfileVisibility: ProfileVisibility { Public | Private }
├── AllowSearch: bool (default true)
├── ConsentToTerms: bool
├── TermsConsentDate: DateTime
├── UpdatedAt: DateTime
```

**Phase 1 scope:** Profile visibility (Public/Private) and search visibility toggle.

**Phase 2 additions:**
- `AllowPersonalizedRecommendations: bool` (default true)
- `AllowAnalyticsTracking: bool` (default true)
- `DataExportRequestedAt: DateTime?` (GDPR data export)

**Business Rules:**
- Private profiles hide storefront from browse (for sellers) but reviews remain visible (reviews are linked to orders)
- `AllowSearch = false` excludes user/storefront from `/search` results
- FriendsOnly visibility deferred — simplified to Public/Private for Phase 1

### 9.7 UserFavoriteSeller Entity (Catalog)

```
UserFavoriteSeller (Entity, in catalog schema)
├── Id: Guid
├── UserId: Guid (not a nav prop — cross-module FK)
├── StorefrontId: Guid (FK to Storefront)
├── FollowedAt: DateTime
```

**Business Rules:**
- UserId is a logical FK to Identity (no DB FK across schemas per ADR-004)
- One follow per user-storefront pair (unique index on UserId + StorefrontId)
- Sale notifications (NotifyOnSale) deferred to Phase 2 — keep entity lean

### 9.8 WishlistItem Entity (Cart)

```
WishlistItem (Entity, in cart schema or Redis)
├── Id: Guid
├── UserId: Guid
├── VariantId: Guid
├── ProductTitle: string (snapshot at save time)
├── ProductSlug: string (for linking)
├── StorefrontSlug: string (for linking)
├── SnapshotPrice: Money (price when saved)
├── AddedAt: DateTime
```

**Business Rules:**
- Wishlist is NOT cart — separate entity, no reservation, no TTL
- Duplicate VariantId per user is ignored (upsert)
- Price drift: when wishlist is displayed, fetch current price and show delta badge
- Phase 2: shareable wishlist link via `/wishlist/{hashedUserId}`

### 9.9 UserShippingPreference Entity (Orders)

```
UserShippingPreference (Entity, 1:1 with User, in orders schema)
├── Id: Guid
├── UserId: Guid
├── PreferredSpeed: ShippingSpeed { Standard | Express | Any }
├── MaxShippingCostTolerance: decimal? (optional, in user's CurrencyDisplay)
├── UpdatedAt: DateTime
```

**Business Rules:**
- Used during checkout to show warnings (NOT to block purchase)
- "This seller charges $15 shipping, your max preference is $10" — warning only
- UserId is a logical FK (cross-module, no DB FK)

### 9.10 OrderPreference Entity (Orders)

```
OrderPreference (Entity, 1:1 with User, in orders schema)
├── Id: Guid
├── UserId: Guid
├── NotificationDelay: NotificationDelay { Immediate | OneHour | Daily }
├── DisputePreference: DisputeResolutionPreference { DirectNegotiation | AdminArbitration }
├── AutoAcceptSellerOffers: bool (default false)
├── UpdatedAt: DateTime
```

**Business Rules:**
- NotificationDelay overrides global NotificationFrequency specifically for order events
- DisputePreference influences dispute UI (direct chat vs formal arbitration) — informational, not enforced
- AutoAcceptSellerOffers: if true, seller's resolution offers in disputes auto-accept after 48h

### 9.11 Phase 2 Entities (Stub Now, Implement Later)

**UserSession (Identity, Phase 2):**
```
UserSession
├── Id, UserId, RefreshTokenHash, DeviceFingerprint, DeviceName
├── CreatedAt, LastActivityAt, ExpiresAt
```
- Active session management, "sign out other devices"
- Auto-expire after 30 days inactivity

**UserTwoFactorAuth (Identity, Phase 2):**
```
UserTwoFactorAuth
├── Id, UserId, SecretKey (encrypted), IsEnabled, BackupCodes
├── CreatedAt
```
- TOTP-based 2FA (Google Authenticator compatible)
- Backup codes shown once on setup

**PaymentMethod (Payments, Phase 2+):**
```
PaymentMethod
├── Id, UserId, CardType, Last4Digits, ExpiryMonth, ExpiryYear, IsDefault
├── CreatedAt
```
- Never store full card number — tokenized via payment gateway

### 9.12 Settings Cross-Module Contracts

These contracts go in `MarketNest.Core/Contracts/`:

```csharp
// IUserPreferencesReadService — any module that needs user preferences
public interface IUserPreferencesReadService
{
    Task<UserPreferencesSnapshot?> GetByUserIdAsync(Guid userId, CancellationToken ct);
}

// INotificationPreferenceReadService — Notifications module checks before sending
public interface INotificationPreferenceReadService
{
    Task<NotificationPreferenceSnapshot?> GetByUserIdAsync(Guid userId, CancellationToken ct);
}
```

**Snapshot records** (in Core, cross-module DTOs):
```csharp
public record UserPreferencesSnapshot(
    string Timezone, string TimeFormat, string DateFormat, string CurrencyDisplay, string Language);

public record NotificationPreferenceSnapshot(
    bool NotifyOrderPlaced, bool NotifyOrderConfirmed, bool NotifyOrderShipped,
    bool NotifyOrderDelivered, bool NotifyDisputeOpened, bool NotifyDisputeResolved,
    bool NotifyReviewReceived, bool NotifyPaymentProcessed, bool SubscribeNewsletter,
    NotificationFrequency Frequency, string? AlternateEmail, bool AlternateEmailVerified,
    NotificationTarget NotificationTarget);
```

### 9.13 Settings UI Architecture

```
/account/settings — Razor Page with HTMX tab switching

Tab navigation (left sidebar on desktop, top tabs on mobile):
  Tab 1: Profile           → GET /account/settings/profile (partial)
  Tab 2: Addresses          → GET /account/settings/addresses (partial)
  Tab 3: Preferences        → GET /account/settings/preferences (partial)
  Tab 4: Communications     → GET /account/settings/communications (partial)
  Tab 5: Security           → GET /account/settings/security (partial)
  Tab 6: Privacy            → GET /account/settings/privacy (partial)
  Tab 7: Shipping           → GET /account/settings/shipping (partial, buyer only)
  Tab 8: Favorites          → GET /account/settings/favorites (partial, buyer only)
  Tab 9: Order Preferences  → GET /account/settings/order-preferences (partial)

Each tab: HTMX GET loads partial → form submission POST → success toast + reload partial.
```

### 9.14 Phase Implementation Summary

**Phase 1 — Must Build (foundations that prevent future refactoring):**
- [ ] User profile extensions (PhoneNumber, AvatarFileId, PublicBio)
- [ ] UserAddress entity + CRUD + default management
- [ ] UserPreferences entity + `IUserTimeZoneProvider` DB-backed implementation
- [ ] NotificationPreference entity + toggle UI (digest batching can be simple)
- [ ] UserPrivacy entity (Public/Private + search toggle)
- [ ] UserFavoriteSeller entity (follow/unfollow)
- [ ] WishlistItem entity (add/remove/list)
- [ ] UserShippingPreference entity (preference + checkout warning)
- [ ] OrderPreference entity (notification delay + dispute preference)
- [ ] Cross-module contracts (`IUserPreferencesReadService`, `INotificationPreferenceReadService`)
- [ ] Settings page with HTMX tabs (all 9 tabs, even if some are minimal)
- [ ] Security tab: password change only

**Phase 2 — Extend (no schema changes needed, just new features on existing tables):**
- [ ] Language preference wired to ASP.NET Core localization
- [ ] 2FA (TOTP) setup/disable
- [ ] Active sessions management
- [ ] Account deletion (soft-delete + 30-day retention)
- [ ] Data export (GDPR)
- [ ] Recommendation/analytics consent toggles
- [ ] Payment methods (requires payment gateway integration)
- [ ] Wishlist sharing via hashed URL
- [ ] Favorite seller sale notifications
