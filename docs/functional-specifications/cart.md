# Cart Module — Functional Specification

> Module: `MarketNest.Cart` | Schema: `cart` | Version: 1.0 | Date: 2026-05-01

## Module Overview

The Cart module manages the buyer's shopping cart and wishlist. It handles item addition/removal, quantity management, inventory reservation via Redis TTL, price drift detection, and checkout initiation.

## Actors

| Actor | Relevant Actions |
|-------|-----------------|
| Guest | Session-local cart (not persisted) |
| Buyer | Persistent cart, wishlist, checkout initiation |

---

## US-CART-001: Add Item to Cart

**As a** buyer, **I want to** add a product variant to my cart, **so that** I can purchase it later.

### Acceptance Criteria

- [ ] Given the variant is Active and in stock, When I add it to my cart, Then a CartItem is created with `SnapshotPrice = EffectivePrice()`
- [ ] Given the variant is already in my cart, When I add it again, Then the quantity is incremented (merged)
- [ ] Given the variant has insufficient stock, When I try to add, Then I see "Only X units available"
- [ ] Given my cart already has 20 distinct items, When I try to add another, Then I see "Cart is full (max 20 items)"
- [ ] Given the cart is in `CheckedOut` status, When I try to add, Then I see "Cart is being processed"
- [ ] Given the item is added, Then a Redis reservation key is set with 15-minute TTL

### Business Rules

- Max 20 distinct items per cart
- Max 99 quantity per item
- Adding same VariantId merges (increases quantity)
- Cannot add to a CheckedOut cart
- SnapshotPrice captures `EffectivePrice()` at time of add
- Reservation: atomic increment of `quantityReserved` in DB + Redis key with 15-min TTL

### Technical Notes

- Redis key: `marketnest:cart:{userId}:reservation:{variantId}` = qty, TTL=15min
- Domain event: `CartItemAddedEvent`
- Stock check: `quantityAvailable ≥ requested qty` (pessimistic lock)
- Uses `variant.EffectivePrice()` — never raw `Price`

### Priority

Phase 1

---

## US-CART-002: Update Item Quantity

**As a** buyer, **I want to** change the quantity of an item in my cart, **so that** I can buy more or fewer units.

### Acceptance Criteria

- [ ] Given I increase quantity and stock is available, When saved, Then quantity is updated and reservation adjusted
- [ ] Given I increase quantity beyond available stock, When saved, Then I see "Only X units available"
- [ ] Given I decrease quantity, When saved, Then reservation is decremented accordingly
- [ ] Given quantity set to 0, Then the item is removed from cart (same as US-CART-003)
- [ ] Given quantity > 99, Then I see "Maximum 99 per item"

### Business Rules

- Quantity range: 1–99 per item
- Stock availability re-checked on quantity increase
- Reservation adjustment: increment/decrement atomically
- Setting quantity to 0 removes the item

### Technical Notes

- Reservation delta: adjust Redis TTL key and DB quantityReserved
- Validate against current stock (not snapshot)

### Priority

Phase 1

---

## US-CART-003: Remove Item from Cart

**As a** buyer, **I want to** remove an item from my cart, **so that** I no longer intend to purchase it.

### Acceptance Criteria

- [ ] Given I have an item in my cart, When I click remove, Then the item is deleted from the cart
- [ ] Given the item had a reservation, When removed, Then the DB quantityReserved is decremented and Redis key deleted
- [ ] Given removal succeeds, Then the cart total updates immediately

### Business Rules

- Reservation must be released on removal
- Domain event: `CartItemRemovedEvent`

### Technical Notes

- Release reservation: decrement `quantityReserved` in DB + delete Redis key
- HTMX: partial cart update after removal

### Priority

Phase 1

---

## US-CART-004: View Cart with Price Drift Detection

**As a** buyer, **I want to** view my cart with current prices and be warned if prices have changed significantly, **so that** I'm aware of price differences before checkout.

### Acceptance Criteria

- [ ] Given I view my cart, Then each item shows both the snapshot price (at time of add) and the current live price
- [ ] Given the current price differs from snapshot by > 5%, Then a warning badge is displayed on that item
- [ ] Given prices have increased, Then the warning says "Price increased since added"
- [ ] Given prices have decreased, Then the warning says "Price decreased — you'll pay the current price"
- [ ] Given the cart is displayed, Then the total is calculated from current `EffectivePrice()` values

### Business Rules

- Price drift threshold: 5% (configurable)
- Cart total uses live `EffectivePrice()` — not snapshot
- Snapshot shown for reference/comparison only
- Drift warning is informational only — does not block checkout

### Technical Notes

- `CurrentPrice` is fetched live (not stored) — cross-module call to Catalog
- Drift calculation: `|CurrentPrice - SnapshotPrice| / SnapshotPrice > 0.05`
- UI: badge/tooltip on cart item row

### Priority

Phase 1

---

## US-CART-005: Reservation TTL Refresh (Heartbeat)

**As the** platform, **I want** cart page views to refresh the reservation TTL, **so that** active shoppers don't lose their reserved items.

### Acceptance Criteria

- [ ] Given a buyer is viewing their cart page, When the page loads or heartbeat fires, Then all reservation TTLs are reset to 15 minutes
- [ ] Given the buyer navigates away, Then no heartbeat fires and TTL counts down normally
- [ ] Given the TTL refresh succeeds, Then the Redis EXPIRE command resets the key

### Business Rules

- Each cart page view / heartbeat refreshes TTL (`EXPIRE` reset)
- Heartbeat interval: configurable (suggested every 5 minutes via JS)
- Only refreshes for items currently in the active cart

### Technical Notes

- Client-side: periodic HTMX/fetch call to refresh endpoint
- Server: iterate cart items, call `EXPIRE` on each Redis key
- No domain event needed

### Priority

Phase 1

---

## US-CART-006: Reservation Release on TTL Expiry

**As the** platform, **I want** expired reservations to be automatically released, **so that** stock becomes available to other buyers.

### Acceptance Criteria

- [ ] Given a reservation Redis key expires (TTL = 0), Then `quantityReserved` is decremented in DB
- [ ] Given the cleanup job runs, Then any reservations older than 20 minutes without a Redis key are released
- [ ] Given reservation is released, Then the variant's available quantity increases accordingly

### Business Rules

- Redis keyspace notification triggers release (primary path)
- Backup: `CleanupExpiredReservations` job runs every 5 minutes, releases reservations > 20 min old
- Released stock becomes immediately available to other buyers

### Technical Notes

- Background job: `CleanupExpiredReservations` (job key: `cart.reservation.cleanup`)
- Redis keyspace notifications: `__keyevent@0__:expired` subscription
- Implements `IBackgroundJob` with `JobDescriptor`
- Background jobs manage own transactions

### Priority

Phase 1

---

## US-CART-007: Wishlist (Add/Remove/View)

**As a** buyer, **I want to** save items to a wishlist for later, **so that** I can track products I'm interested in without reserving stock.

### Acceptance Criteria

- [ ] Given I add a variant to my wishlist, When saved, Then a `WishlistItem` record is created with snapshot price
- [ ] Given the variant is already in my wishlist, When I add again, Then it's a no-op (upsert/ignore)
- [ ] Given I view my wishlist, Then current prices are fetched and delta badge shown if price changed
- [ ] Given I remove an item from wishlist, Then the record is deleted
- [ ] Given the product is archived/deleted, Then the wishlist item shows "Product unavailable"

### Business Rules

- Wishlist is NOT cart — no reservation, no TTL, no stock check
- Duplicate variant per user: ignored (upsert behavior)
- Snapshot: product title, slug, storefront slug, price at save time
- Price delta badge: compare snapshot vs current `EffectivePrice()`
- Phase 2: shareable wishlist via URL

### Technical Notes

- Entity: `WishlistItem` (in cart schema or Redis)
- No stock reservation — purely informational
- Cross-module read: current price from Catalog

### Priority

Phase 1

---

## US-CART-008: Cart Checkout Initiation

**As a** buyer, **I want to** initiate checkout from my cart, **so that** I can proceed to place an order.

### Acceptance Criteria

- [ ] Given my cart has ≥1 item and all items are in stock, When I click "Checkout", Then cart status changes to `CheckedOut`
- [ ] Given any item in my cart is out of stock, When I try to checkout, Then I see which items are unavailable
- [ ] Given checkout is initiated, Then no more items can be added to the cart
- [ ] Given checkout is initiated, Then `CartCheckedOutEvent` is raised (Orders module picks up)
- [ ] Given checkout fails or is abandoned, Then cart returns to `Active` status

### Business Rules

- All items re-validated against current stock at checkout time
- Cart status: Active → CheckedOut (on initiate) → Active (on failure/abandon)
- Domain event: `CartCheckedOutEvent` triggers order creation flow
- Price re-validated: BuyerTotal calculated from current `EffectivePrice()` values

### Technical Notes

- Domain event: `CartCheckedOutEvent` → handled by Orders module
- Financial snapshot created at this point (prices locked)
- Reservation TTLs extended/converted to order-level locks
- Cross-module: Orders module listens for this event

### Priority

Phase 1

