# MarketNest — Business Logic Requirements

> Version: 0.1 (Planning) | Status: Draft | Date: 2026-04

---

## 1. Core Business Entities

### 1.1 Actors

| Actor | Description | Permissions |
|-------|-------------|-------------|
| **Guest** | Unauthenticated visitor | Browse catalog, view storefronts |
| **Buyer** | Registered customer | Place orders, write reviews (after purchase), open disputes |
| **Seller** | Merchant with storefront | Manage products/inventory, fulfill orders, respond to disputes |
| **Admin** | Platform operator | Arbitrate disputes, manage commissions, ban users |

---

## 2. Domain: Storefront & Catalog

### 2.1 Storefront Rules
- Each Seller has exactly **one** Storefront
- Storefront has: `name`, `slug` (unique, URL-safe), `banner`, `description`, `status` (Active / Suspended / Closed)
- A Suspended storefront hides all products from public view
- Storefront slug cannot be changed after first activation

### 2.2 Product Rules
- A Product belongs to exactly one Storefront
- Product has: `title`, `description`, `category`, `tags[]`, `status` (Draft / Active / Archived)
- Only Active products appear in search/browse
- A Product must have at least **one** ProductVariant to be published

### 2.3 ProductVariant Rules
- Variants represent SKU-level items (e.g., Size=M, Color=Red)
- Each variant has: `sku` (unique platform-wide), `attributes{}`, `price`, `compareAtPrice`
- Price must be > 0; compareAtPrice (original price) must be > price if set

### 2.4 InventoryItem Rules
- Each ProductVariant has exactly one InventoryItem
- `quantityOnHand` = physical stock
- `quantityReserved` = locked by active cart reservations
- `quantityAvailable` = `quantityOnHand - quantityReserved` (computed, never stored)
- Inventory can never go negative: guard at DB level (check constraint) AND application level

---

## 3. Domain: Cart & Inventory Reservation

### 3.1 Cart Rules
- Each authenticated Buyer has **one** active Cart
- Guest carts are session-local only (not persisted to DB)
- On login, guest cart items are merged into user's existing cart (quantity union, conflict = take higher qty, max = stock available)

### 3.2 Inventory Reservation (TTL via Redis)
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
  - Redis keyspace notification → background service listens → releases DB reservation
  - If Redis key expires but DB reservation not released (crash): 
    scheduled cleanup job runs every 5min, releases reservations older than 20min
```

### 3.3 Cart Constraints
- Max 20 distinct items per cart
- Max 99 quantity per line item
- CartItem price is **snapshot** of variant price at time of add (not live price)
- Price drift warning: show banner if current price differs > 5% from cart snapshot price

---

## 4. Domain: Order

### 4.1 Order State Machine

```
                          ┌──────────────┐
                          │   PENDING    │ ← Created on checkout, payment not yet confirmed
                          └──────┬───────┘
                                 │ Payment confirmed
                          ┌──────▼───────┐
                          │  CONFIRMED   │ ← Seller must act within 48h
                          └──────┬───────┘
                     ┌───────────┴──────────┐
              Seller confirms            Seller cancels
                     │                    │
            ┌────────▼───────┐   ┌────────▼───────┐
            │   PROCESSING   │   │   CANCELLED    │ ← Refund triggered
            └────────┬───────┘   └────────────────┘
                     │ Seller ships
            ┌────────▼───────┐
            │    SHIPPED     │ ← Tracking number required
            └────────┬───────┘
                     │ Buyer confirms receipt (or auto-confirm after 7 days)
            ┌────────▼───────┐
            │   DELIVERED    │
            └────┬───────────┘
       ┌─────────┴──────────┐
  No dispute              Dispute opened within 3 days
       │                        │
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

### 4.2 Order Rules
- An Order belongs to **one Buyer** and may contain items from **multiple Sellers** (grouped into sub-orders / Fulfillments internally)
- Each Fulfillment maps to one Seller's items within the Order
- Fulfillment has its own state tracking (mirrors Order states at seller level)
- Order total = sum of all OrderLine `(price × quantity)` + shipping fee − discount
- Buyer shipping address is **snapshot** at order time (not live from profile)

### 4.3 Auto-Actions (Background Jobs)
| Trigger | Action |
|---------|--------|
| Seller no action within 48h of CONFIRMED | Auto-CANCELLED + Buyer refunded |
| Order in SHIPPED for 30 days, no confirmation | Auto-DELIVERED → auto-COMPLETED |
| Order DELIVERED for 3 days, no dispute | Auto-COMPLETED |
| COMPLETED order | Trigger payout calculation for Seller |

---

## 5. Domain: Payment & Commission

### 5.1 Payment Rules
- Payment is a **record** of Buyer paying for Order — not a real gateway integration (stub in Phase 1, Stripe-compatible interface for Phase 2+)
- Supported methods: `CreditCard`, `BankTransfer` (simulated in Phase 1)
- Payment status: `Pending → Captured → Refunded / Failed`
- **No partial captures** in Phase 1

### 5.2 Commission Engine
```
Platform Commission = Order Subtotal × Commission Rate
  Default rate: 10%
  Seller-specific rate override: configurable by Admin

Seller Payout Amount = Order Subtotal - Platform Commission - Payment Processing Fee (2.9% + $0.30 stub)

Payout Schedule:
  - Payout is HELD until Order reaches COMPLETED state
  - Payout batch runs daily at 02:00 UTC
  - Payout status: Pending → Processing → Paid / Failed
```

### 5.3 Refund Rules
- Full refund on CANCELLED orders: immediately
- Partial refund after dispute: Admin-specified amount
- Refunds must reverse payout if already disbursed (Payout marked as Clawback required)

---

## 6. Domain: Reviews

### 6.1 Review Gate (Anti-Fraud)
```
Can leave review IF:
  ✓ Authenticated Buyer
  ✓ Has an Order containing this Product in COMPLETED state
  ✓ Has NOT already reviewed this Product for this Order
  ✗ NOT if Order is in DISPUTED / REFUNDED state (configurable)
```

### 6.2 Review Rules
- Rating: 1–5 stars (integer, required)
- Text: optional, max 1000 chars
- Photos: up to 3 images (Phase 2)
- Seller can reply once per review (max 500 chars)
- Reviews are immutable after 24h of submission
- ReviewVote: Buyers can mark reviews as "Helpful" (1 vote per Buyer per Review)

### 6.3 Review Aggregation
- Product rating = average of all published ratings (rounded to 1 decimal)
- Storefront rating = weighted average across all Products (weight by volume)
- Recalculation: event-driven on new Review / Review deletion

---

## 7. Domain: Disputes

### 7.1 Dispute Flow

```
1. Buyer opens Dispute (within 3 days of DELIVERED)
   Required: reason category, description, evidence photos (optional)
   Reason categories: NotReceived | NotAsDescribed | Damaged | WrongItem | Other

2. Order moves to DISPUTED state

3. Seller has 72h to respond
   Options: Accept (offer refund) | Contest (provide rebuttal)

4a. Seller accepts:
    → Agreed refund amount set → Admin reviews → REFUNDED

4b. Seller contests OR no response in 72h:
    → Escalated to Admin arbitration

5. Admin reviews both sides
   Decision: FullRefund | PartialRefund | DismissBuyerClaim
   → Order moves to REFUNDED or COMPLETED

6. All dispute messages are immutable (audit trail)
```

### 7.2 Dispute Constraints
- Max 1 open Dispute per Order
- Dispute can only be opened within 3 days of DELIVERED status
- Admin must resolve within 5 business days (SLA — soft target for toy project)
- Both parties can submit evidence (text + up to 5 photos per message)

---

## 8. Platform Rules (Admin)

### 8.1 Seller Eligibility
- Seller must verify email before activating Storefront
- Seller must agree to Seller Terms of Service (checkbox + timestamp stored)

### 8.2 Content Moderation
- Prohibited categories: configurable list (e.g., weapons, adult content)
- Admin can suspend any Storefront or individual Product with reason
- Suspended sellers receive email notification

### 8.3 Platform Fee Configuration
- Commission rate configurable per Seller (Admin only)
- Default rate: 10%
- Rate changes take effect on Orders created after change date

---

## 9. Notification Triggers

| Event | Recipients | Channel |
|-------|-----------|---------|
| Order placed | Buyer + Seller | Email |
| Order confirmed by Seller | Buyer | Email |
| Order shipped (tracking added) | Buyer | Email |
| Order auto-confirmed after 7 days | Buyer | Email |
| Dispute opened | Seller + Admin | Email |
| Seller responds to dispute | Buyer + Admin | Email |
| Admin resolves dispute | Buyer + Seller | Email |
| Payout processed | Seller | Email |
| Review left on storefront | Seller | Email (digest, daily) |
| Password reset requested | User | Email |
| New login from unknown device | User | Email |

---

## 10. Business Invariants (Never Violate)

1. `quantityAvailable` can never be negative
2. An Order total is immutable after CONFIRMED state
3. A Payout can only be generated for a COMPLETED Order
4. A Review can only be created by the Buyer of a COMPLETED Order for that specific Product
5. A Dispute can only be opened within the dispute window (3 days post-DELIVERED)
6. Commission rate at time of Order creation is snapshotted to the Commission record (not recalculated if rate changes later)
7. A Seller cannot modify product prices on Orders that are CONFIRMED or later
