# Orders Module — Functional Specification

> Module: `MarketNest.Orders` | Schema: `orders` | Version: 1.0 | Date: 2026-05-01

## Module Overview

The Orders module manages the complete order lifecycle from placement through fulfillment to completion. It handles the order state machine, financial snapshots, fulfillment tracking, auto-actions (background jobs), and user shipping/order preferences.

## Actors

| Actor | Relevant Actions |
|-------|-----------------|
| Buyer | Place order, confirm delivery, cancel (before processing), view orders |
| Seller | Confirm order, ship (with tracking), cancel |
| Admin | View all orders (via Admin module) |
| System | Auto-cancel, auto-deliver, auto-complete (background jobs) |

---

## US-ORDER-001: Place Order from Cart

**As a** buyer, **I want to** place an order from my checked-out cart, **so that** I can purchase the items.

### Acceptance Criteria

- [ ] Given my cart is in `CheckedOut` status, When the order is placed, Then an Order is created with status `Pending`
- [ ] Given multiple sellers in my cart, Then order lines are grouped by StoreId (for fulfillment)
- [ ] Given shipping address is selected, Then it's snapshotted as immutable `Address` on the order
- [ ] Given financial calculation completes, Then all financial fields are computed and stored (ProductSubtotal, discounts, surcharge, BuyerTotal)
- [ ] Given vouchers are applied, Then `AppliedVouchers` JSON column stores snapshot of each voucher used
- [ ] Given the order is placed, Then `OrderPlacedEvent` is raised

### Business Rules

- Financial snapshot: computed once at checkout, immutable after Confirmed
- Formula: `BuyerTotal = NetProductAmount + NetShippingFee + PaymentSurcharge`
- PaymentSurchargeRate snapshotted from admin config
- CommissionRateSnapshot snapshotted from Storefront.CommissionRate
- All prices use `EffectivePrice()` (sale-aware)
- `OrderPlacedEvent` → Notifications (buyer + seller), Payments module

### Technical Notes

- See §10 (Order Financial Calculation Reference) in domain spec
- Financial fields: ProductSubtotal, PlatformProductDiscount, ShopProductDiscount, GrossShippingFee, PlatformShippingDiscount, ShopShippingDiscount, NetShippingFee, PaymentSurchargeRate, PaymentSurcharge, BuyerTotal
- OrderLine snapshots: VariantId, ProductTitle, VariantAttributes, UnitPrice, Quantity, LineTotal, StoreId
- Fulfillment records: one per seller in the order
- Invariant 2: financial fields immutable after CONFIRMED

### Priority

Phase 1

---

## US-ORDER-002: Payment Confirmed → CONFIRMED

**As the** platform, **I want** an order to advance to CONFIRMED when payment is captured, **so that** the seller can begin processing.

### Acceptance Criteria

- [ ] Given the order is in `Pending` status, When `PaymentCapturedEvent` is received, Then status changes to `Confirmed`
- [ ] Given status changes to Confirmed, Then `ConfirmedAt` timestamp is recorded
- [ ] Given the status change, Then `OrderConfirmedEvent` is raised
- [ ] Given the seller doesn't act within 48h, Then auto-cancellation is triggered (US-ORDER-010)

### Business Rules

- Only `Pending` → `Confirmed` transition on payment capture
- Seller must act within 48 hours of confirmation
- `OrderConfirmedEvent` → Notifications (buyer)

### Technical Notes

- Event handler: listens for `PaymentCapturedEvent` from Payments module
- Domain event: `OrderConfirmedEvent`
- Starts the 48h seller action timer

### Priority

Phase 1

---

## US-ORDER-003: Seller Confirms → PROCESSING

**As a** seller, **I want to** confirm that I'm processing an order, **so that** the buyer knows their order is being prepared.

### Acceptance Criteria

- [ ] Given the order is `Confirmed` and I am the owning seller, When I confirm processing, Then fulfillment status changes to `Processing`
- [ ] Given I am not the owning seller, When I try to confirm, Then I see 403 Forbidden
- [ ] Given the order is not in `Confirmed` status, When I try to confirm, Then I see "Invalid state transition"

### Business Rules

- Only the seller whose items are in the order can confirm
- Valid transition: Confirmed → Processing
- Resets the auto-cancel timer

### Technical Notes

- Fulfillment-level status change (per seller)
- Order-level status: stays at Confirmed until all sellers confirm, or moves to Processing

### Priority

Phase 1

---

## US-ORDER-004: Seller Ships (Tracking Required) → SHIPPED

**As a** seller, **I want to** mark an order as shipped with tracking information, **so that** the buyer can track their delivery.

### Acceptance Criteria

- [ ] Given the order is `Processing`, When I submit shipping with tracking number and URL, Then status changes to `Shipped`
- [ ] Given no tracking number is provided, When I try to ship, Then I see "Tracking number is required"
- [ ] Given shipment is recorded, Then `ShippedAt` timestamp and tracking details are stored
- [ ] Given the status changes, Then `OrderShippedEvent` is raised (notification to buyer)

### Business Rules

- Tracking number and tracking URL are required
- Valid transition: Processing → Shipped
- `OrderShippedEvent` → Notifications (buyer with tracking link)

### Technical Notes

- Fulfillment entity: stores TrackingNumber, TrackingUrl, ShippedAt
- Domain event: `OrderShippedEvent`
- Auto-delivery timer starts (30 days)

### Priority

Phase 1

---

## US-ORDER-005: Buyer Confirms Delivery → DELIVERED

**As a** buyer, **I want to** confirm that I received my order, **so that** the order can be completed and seller paid.

### Acceptance Criteria

- [ ] Given the order is `Shipped`, When I confirm delivery, Then status changes to `Delivered`
- [ ] Given delivery is confirmed, Then `DeliveredAt` timestamp is recorded
- [ ] Given status changes, Then `OrderDeliveredEvent` is raised
- [ ] Given delivery is confirmed, Then the 3-day dispute window begins

### Business Rules

- Valid transition: Shipped → Delivered
- Only the buyer can confirm delivery
- Dispute window: 3 days from `DeliveredAt`
- `OrderDeliveredEvent` → Notifications, starts dispute window timer

### Technical Notes

- Domain event: `OrderDeliveredEvent`
- Auto-complete timer starts (3 days from delivery)

### Priority

Phase 1

---

## US-ORDER-006: Auto-Delivery After 30 Days Shipped

**As the** platform, **I want** orders shipped for 30+ days without buyer confirmation to auto-transition to DELIVERED, **so that** orders don't remain in limbo.

### Acceptance Criteria

- [ ] Given an order has been in `Shipped` status for > 30 days, When `AutoConfirmShippedOrders` job runs, Then status changes to `Delivered`
- [ ] Given auto-delivery occurs, Then `DeliveredAt` is set to job execution time
- [ ] Given auto-delivery occurs, Then the 3-day dispute window starts

### Business Rules

- Threshold: 30 days in Shipped status
- Job schedule: Daily at 01:00 UTC
- Dispute window still applies (3 days from auto-delivery)

### Technical Notes

- Background job: `AutoConfirmShippedOrders`
- Job key: `orders.auto-confirm-shipped`
- Schedule: Daily 01:00 UTC
- Manages own transactions (outside HTTP pipeline)

### Priority

Phase 1

---

## US-ORDER-007: Auto-Complete After 3 Days Delivered

**As the** platform, **I want** delivered orders without disputes to auto-complete after 3 days, **so that** sellers get paid promptly.

### Acceptance Criteria

- [ ] Given an order has been `Delivered` for 3 days with no dispute opened, When `AutoCompleteOrders` job runs, Then status changes to `Completed`
- [ ] Given auto-complete occurs, Then `CompletedAt` is set and `OrderCompletedEvent` is raised
- [ ] Given `OrderCompletedEvent` is raised, Then Payments module schedules payout

### Business Rules

- Threshold: 3 days in Delivered status without dispute
- If dispute opened within 3 days: order moves to Disputed instead
- `OrderCompletedEvent` → Payments (schedule payout)

### Technical Notes

- Background job: `AutoCompleteOrders`
- Job key: `orders.auto-complete`
- Schedule: Daily 01:05 UTC
- Domain event: `OrderCompletedEvent`

### Priority

Phase 1

---

## US-ORDER-008: Seller Cancels Order

**As a** seller, **I want to** cancel an order I cannot fulfill, **so that** the buyer gets a refund.

### Acceptance Criteria

- [ ] Given the order is in `Confirmed` or `Processing` status, When I cancel with a reason, Then status changes to `Cancelled`
- [ ] Given cancellation occurs, Then `CancelledAt` and `CancellationReason` are recorded
- [ ] Given `OrderCancelledEvent` is raised, Then Payments module processes a full refund
- [ ] Given `OrderCancelledEvent` is raised, Then inventory reservations are released
- [ ] Given the order is already `Shipped`, Then seller cannot cancel

### Business Rules

- Seller can cancel: Confirmed or Processing status only
- Must provide cancellation reason
- Full refund to buyer (Payment.ChargedAmount)
- Inventory released back to available stock
- Cannot cancel after shipping

### Technical Notes

- Domain event: `OrderCancelledEvent` → Payments (refund), Inventory (release), Notifications
- `VoucherUsageReversedEvent` if vouchers were applied

### Priority

Phase 1

---

## US-ORDER-009: Buyer Cancels Order

**As a** buyer, **I want to** cancel my order before the seller starts processing, **so that** I can get a refund if I changed my mind.

### Acceptance Criteria

- [ ] Given the order is in `Confirmed` status (seller hasn't started processing), When I cancel, Then status changes to `Cancelled`
- [ ] Given the order is in `Processing` or later status, When I try to cancel, Then I see "Order already being processed — contact seller"
- [ ] Given buyer cancellation succeeds, Then full refund is processed

### Business Rules

- Buyer can only cancel before seller starts processing (Confirmed status only)
- After Processing/Shipped/Delivered: buyer must open a dispute instead
- Same refund and inventory release logic as seller cancel

### Technical Notes

- Same domain event: `OrderCancelledEvent`
- CancellationReason: "Buyer requested cancellation"

### Priority

Phase 1

---

## US-ORDER-010: Auto-Cancel Unconfirmed After 48h

**As the** platform, **I want** orders where the seller hasn't acted within 48h to be automatically cancelled, **so that** buyers aren't left waiting indefinitely.

### Acceptance Criteria

- [ ] Given an order has been `Confirmed` for > 48 hours without seller action, When `AutoCancelUnconfirmedOrders` runs, Then status changes to `Cancelled`
- [ ] Given auto-cancellation occurs, Then buyer is fully refunded
- [ ] Given auto-cancellation occurs, Then both buyer and seller are notified

### Business Rules

- Threshold: 48 hours in Confirmed status with no seller action
- Full refund to buyer
- Reason: "Seller did not respond within 48 hours"
- Job schedule: every 30 minutes

### Technical Notes

- Background job: `AutoCancelUnconfirmedOrders`
- Job key: `orders.auto-cancel-unconfirmed`
- Schedule: every 30 minutes
- Domain event: `OrderCancelledEvent`

### Priority

Phase 1

---

## US-ORDER-011: View Order Details

**As a** buyer or seller, **I want to** view complete order details including financial breakdown, **so that** I understand what was charged/received.

### Acceptance Criteria

- [ ] Given I am the buyer, When I view my order, Then I see: items, quantities, prices, discounts, shipping, surcharge, and BuyerTotal
- [ ] Given I am the seller, When I view an order, Then I see: my items, commission breakdown, and expected payout
- [ ] Given the order has tracking info, Then tracking number and URL are displayed with a link
- [ ] Given vouchers were applied, Then the discount breakdown shows which vouchers and how much each saved

### Business Rules

- Buyer sees: full BuyerTotal breakdown
- Seller sees: their portion, commission, expected payout (Payout aggregate data)
- All financial data is from snapshots (immutable)

### Technical Notes

- `CheckoutSummaryDto`: exposes full financial breakdown
- Query: `GetOrderDetailsQuery` with role-based projection

### Priority

Phase 1

---

## US-ORDER-012: Shipping Preference Warning at Checkout

**As a** buyer, **I want to** see a warning if shipping costs exceed my preferences, **so that** I can make an informed decision.

### Acceptance Criteria

- [ ] Given my shipping preference max is $10 and the order shipping is $15, When I view checkout summary, Then I see a warning "Shipping exceeds your preference ($10)"
- [ ] Given the warning is shown, Then I can still proceed with checkout (warning only, not blocking)
- [ ] Given I have no shipping preference set, Then no warning is shown

### Business Rules

- Warning is informational only — does NOT block purchase
- Uses `UserShippingPreference.MaxShippingCostTolerance`
- Preference is per-user, module-owned by Orders

### Technical Notes

- Entity: `UserShippingPreference` (orders schema)
- Cross-module: reads shipping fee calculated at checkout
- UI: warning banner/toast in checkout summary

### Priority

Phase 1

---

## US-ORDER-013: Order Preferences (Notification Delay, Dispute Preference)

**As a** buyer, **I want to** set my order-related preferences, **so that** the platform respects my communication and dispute handling style.

### Acceptance Criteria

- [ ] Given I set notification delay to "Daily", When order events occur, Then notifications are batched
- [ ] Given I set dispute preference to "Admin Arbitration", Then the dispute UI defaults to formal process
- [ ] Given I enable "Auto-accept seller offers", Then seller resolution offers auto-accept after 48h

### Business Rules

- `NotificationDelay` overrides global frequency for order events specifically
- `DisputePreference`: informational (affects UI default, not enforcement)
- `AutoAcceptSellerOffers`: if true, seller offers in disputes auto-accepted after 48h

### Technical Notes

- Entity: `OrderPreference` (1:1 with User, orders schema)
- Settings tab: "Order Preferences" in user settings

### Priority

Phase 1

---

## US-ORDER-014: Order State Machine Enforcement

**As the** platform, **I want** invalid state transitions to be rejected, **so that** order integrity is maintained.

### Acceptance Criteria

- [ ] Given an order in `Pending`, Then only allowed transition is → Confirmed (via payment) or Cancelled
- [ ] Given an order in `Confirmed`, Then allowed transitions are → Processing, Cancelled
- [ ] Given an order in `Processing`, Then allowed transition is → Shipped, Cancelled (seller only)
- [ ] Given an order in `Shipped`, Then allowed transitions are → Delivered
- [ ] Given an order in `Delivered`, Then allowed transitions are → Completed, Disputed
- [ ] Given an order in `Completed` or `Refunded`, Then no further transitions are allowed

### Business Rules

- State machine enforced at domain level (domain method guards)
- Invalid transitions throw domain exceptions
- See Order State Machine diagram in domain spec §3.4

### Technical Notes

- Domain guard methods on Order aggregate
- Each transition records timestamp (ConfirmedAt, ShippedAt, etc.)
- Status enum: Pending | Confirmed | Processing | Shipped | Delivered | Completed | Cancelled | Disputed | Refunded

### Priority

Phase 1

