# Promotions Module — Functional Specification

> Module: `MarketNest.Promotions` | Schema: `promotions` | Version: 1.0 | Date: 2026-05-01

## Module Overview

The Promotions module manages vouchers (coupons/discount codes). It supports platform-wide vouchers created by admins and shop-specific vouchers created by sellers. The module handles voucher lifecycle, validation at checkout, stacking rules, and usage tracking.

## Actors

| Actor | Relevant Actions |
|-------|-----------------|
| Admin | Create/manage platform vouchers, pause any voucher |
| Seller | Create/manage shop vouchers for own store |
| Buyer | Apply vouchers at checkout |
| System | Auto-expire/deplete vouchers (background job) |

---

## US-PROMO-001: Admin Creates Platform Voucher

**As an** admin, **I want to** create a platform-wide voucher, **so that** the entire marketplace can benefit from a promotion.

### Acceptance Criteria

- [ ] Given I am an admin, When I create a voucher with Scope=Platform, Then it's created in `Draft` status
- [ ] Given I provide a code that already exists, When submitted, Then I see "Voucher code already exists"
- [ ] Given I set DiscountType=PercentageOff with value 50 and MaxDiscountCap=$20, Then validation passes
- [ ] Given I set EffectiveDate after ExpiryDate, When submitted, Then I see "Effective date must be before expiry"
- [ ] Given I set DiscountValue > 100 for PercentageOff, Then I see "Percentage must be between 1 and 100"
- [ ] Given MaxDiscountCap is set for FixedAmount type, Then I see "Max discount cap only valid for percentage discounts on products"

### Business Rules

- Admin can ONLY create Platform scope vouchers (invariant V4)
- Code: globally unique, uppercase, 6–20 chars (`^[A-Z0-9\-]{6,20}$`)
- DiscountType: PercentageOff (1–100) | FixedAmount (> 0)
- ApplyFor: ProductSubtotal | ShippingFee
- MaxDiscountCap: only valid for PercentageOff + ProductSubtotal
- EffectiveDate < ExpiryDate; ExpiryDate must be future; max window 2 years
- Created in Draft status — must be explicitly activated

### Technical Notes

- `VoucherCode` value object: uppercase validation, uniqueness
- Domain event: `VoucherCreatedEvent`
- Invariants: V4, V7, V8, V11

### Priority

Phase 1

---

## US-PROMO-002: Seller Creates Shop Voucher

**As a** seller, **I want to** create a discount voucher for my own store, **so that** I can attract buyers to my products.

### Acceptance Criteria

- [ ] Given I am a seller, When I create a voucher with Scope=Shop and my StoreId, Then it's created in Draft status
- [ ] Given I try to create a Platform voucher, Then I see "Sellers can only create shop vouchers"
- [ ] Given I try to create a voucher for another seller's store, Then I see 403 Forbidden
- [ ] Given all validation passes, Then voucher is saved with my StoreId

### Business Rules

- Seller can ONLY create Shop scope vouchers (invariant V5)
- StoreId must match seller's own storefront
- Same validation rules as platform voucher (code, dates, discount)
- Shop voucher only applies to items from matching StoreId at checkout

### Technical Notes

- Ownership check: voucher.StoreId = seller's storefront ID
- Domain event: `VoucherCreatedEvent`
- Invariants: V3, V5

### Priority

Phase 1

---

## US-PROMO-003: Activate Voucher

**As a** voucher creator (admin or seller), **I want to** activate a draft voucher, **so that** it becomes usable at checkout.

### Acceptance Criteria

- [ ] Given a voucher is in Draft status, When I activate it, Then status changes to `Active`
- [ ] Given the EffectiveDate hasn't arrived yet, Then the voucher is active but not yet applicable (checked at validation time)
- [ ] Given activation succeeds, Then `VoucherActivatedEvent` is raised
- [ ] Given the voucher is not in Draft status, Then I see "Can only activate Draft vouchers"

### Business Rules

- Status transition: Draft → Active
- Active but before EffectiveDate: exists but not applicable at checkout
- Only the creator (or admin for any voucher) can activate

### Technical Notes

- Domain event: `VoucherActivatedEvent`
- Status: Draft → Active (only valid transition for activation)

### Priority

Phase 1

---

## US-PROMO-004: Pause Voucher

**As a** voucher owner or admin, **I want to** pause an active voucher, **so that** it temporarily stops being usable.

### Acceptance Criteria

- [ ] Given a voucher is Active, When I pause it, Then status changes to `Paused`
- [ ] Given a paused voucher, Then it fails validation at checkout ("Voucher is currently paused")
- [ ] Given I reactivate a paused voucher, Then status returns to `Active`
- [ ] Given admin pauses a shop voucher, Then the seller is notified

### Business Rules

- Admin can pause any voucher (Platform or Shop)
- Seller can pause only their own shop vouchers
- Paused: not usable at checkout
- Can be reactivated: Paused → Active
- `VoucherPausedEvent` → Notify seller (if paused by admin)

### Technical Notes

- Domain event: `VoucherPausedEvent`
- Status transitions: Active ↔ Paused

### Priority

Phase 1

---

## US-PROMO-005: Apply Voucher at Checkout

**As a** buyer, **I want to** apply a voucher code at checkout, **so that** I receive a discount on my order.

### Acceptance Criteria

- [ ] Given I enter a valid active voucher code, When validated, Then the discount is calculated and shown
- [ ] Given the voucher is expired/paused/depleted, When I enter the code, Then I see "Voucher is not available"
- [ ] Given the voucher has a MinOrderValue of $50 and my subtotal is $30, Then I see "Minimum order of $50 required"
- [ ] Given a shop voucher for Store A and my cart has items from Store A and Store B, Then discount applies only to Store A items
- [ ] Given PercentageOff with MaxDiscountCap, Then discount is capped at the cap amount
- [ ] Given FixedAmount voucher, Then discount is min(DiscountValue, applicable target)

### Business Rules

- **Validation sequence:**
  1. Code exists → Status = Active → within EffectiveDate..ExpiryDate
  2. UsageCount < UsageLimit (if limited)
  3. Per-user count < UsageLimitPerUser (if limited)
  4. Scope check: Shop voucher only applies to matching StoreId items
  5. MinOrderValue ≤ applicable ProductSubtotal
  6. Calculate discount per two-axis model
- Discount never makes a component negative
- ProductDiscount ≤ applicable ProductSubtotal
- ShippingDiscount ≤ GrossShippingFee
- Invariants: V2, V3, V9, V10

### Technical Notes

- `IVoucherService.ValidateAsync()` returns `DiscountResult`
- Two-axis model: DiscountType × ApplyFor
- Discount calculation in Promotions module, consumed by Orders at checkout
- `AppliedVoucherSnapshot` stored on Order as JSON column

### Priority

Phase 1

---

## US-PROMO-006: Voucher Stacking Rules

**As the** platform, **I want** voucher stacking rules to be enforced, **so that** discounts remain financially sustainable.

### Acceptance Criteria

- [ ] Given a buyer applies 1 platform voucher and 1 shop voucher from Store A, Then both are accepted
- [ ] Given a buyer tries to apply 2 platform vouchers, Then the second is rejected "Only one platform voucher allowed"
- [ ] Given a buyer tries to apply 2 shop vouchers from the same store, Then the second is rejected
- [ ] Given a multi-seller order, Then each seller's items can have their own shop voucher
- [ ] Given stacking rules pass, Then discounts are calculated independently and summed

### Business Rules

- Max 1 Platform voucher per checkout
- Max 1 Shop voucher per shop per checkout
- Multi-seller orders: each shop may have independent shop voucher
- Platform + Shop vouchers stack (both can apply simultaneously)
- Invariants: V12, V13

### Technical Notes

- CheckoutHandler validates stacking before order creation
- Platform voucher: applies across all items
- Shop voucher: applies only to items from matching StoreId

### Priority

Phase 1

---

## US-PROMO-007: Per-User Usage Limit

**As the** platform, **I want** per-user voucher usage limits enforced, **so that** vouchers can't be abused by single users.

### Acceptance Criteria

- [ ] Given a voucher has UsageLimitPerUser=1 and I've already used it, When I try again, Then I see "You've already used this voucher"
- [ ] Given UsageLimitPerUser is null (unlimited), Then no per-user restriction applies
- [ ] Given I used the voucher but the order was cancelled/refunded, Then my usage count is decremented (can use again)

### Business Rules

- Per-user check: count VoucherUsage records for this user + voucher
- Cancelled/refunded orders: UsageCount decremented (restored availability)
- `UsageLimitPerUser`: null = unlimited
- Check happens as step 3 of validation sequence

### Technical Notes

- Query: `VoucherUsage` records WHERE VoucherId=X AND UserId=Y
- Count against active (non-reversed) usages
- Invariant V2: UsageCount never exceeds UsageLimit

### Priority

Phase 1

---

## US-PROMO-008: Auto-Expire/Deplete Background Job

**As the** platform, **I want** expired or depleted vouchers to be automatically status-updated, **so that** they can't be accidentally applied.

### Acceptance Criteria

- [ ] Given a voucher's ExpiryDate has passed, When the job runs, Then status changes to `Expired`
- [ ] Given a voucher's UsageCount ≥ UsageLimit, When the job runs, Then status changes to `Depleted`
- [ ] Given expiry/depletion occurs, Then `VoucherExpiredEvent` or `VoucherDepletedEvent` is raised
- [ ] Given the job runs hourly, Then no voucher remains Active more than ~1 hour past its expiry

### Business Rules

- Schedule: hourly
- Expired: past ExpiryDate → Status = Expired
- Depleted: UsageCount ≥ UsageLimit → Status = Depleted
- Events → Notifications (seller informed their voucher expired/depleted)

### Technical Notes

- Background job: `VoucherExpiryJob`
- Job key: `promotions.voucher.expiry`
- Schedule: every hour
- Domain events: `VoucherExpiredEvent`, `VoucherDepletedEvent`
- Manages own transactions (outside HTTP pipeline)

### Priority

Phase 1

---

## US-PROMO-009: Reverse Usage on Order Cancel/Refund

**As the** platform, **I want** voucher usage to be reversed when an order is cancelled or refunded, **so that** customers can use the voucher again.

### Acceptance Criteria

- [ ] Given an order with a voucher is cancelled, When processed, Then voucher's UsageCount is decremented
- [ ] Given the usage is reversed, Then `VoucherUsageReversedEvent` is raised
- [ ] Given the VoucherUsage record, Then it's kept in the database (audit) but usage count is decremented
- [ ] Given a user's per-user count was at the limit, After reversal they can apply the voucher again

### Business Rules

- Trigger: `OrderCancelledEvent` or `OrderRefundedEvent`
- VoucherUsage record: retained for audit (not deleted)
- UsageCount: decremented by 1 per reversed usage
- Buyer can re-use the voucher if limits allow

### Technical Notes

- Event handler: listens for `OrderCancelledEvent` / via `VoucherUsageReversedEvent`
- Cross-module event chain: Orders → Promotions
- Audit: VoucherUsage record kept (not soft-deleted)

### Priority

Phase 1

---

## US-PROMO-010: Immutability After First Usage

**As the** platform, **I want** key voucher fields to be locked after the first use, **so that** active promotions remain consistent.

### Acceptance Criteria

- [ ] Given a voucher has been used (VoucherUsage records exist), When I try to change DiscountType, Then I see "Cannot modify after voucher has been used"
- [ ] Given a voucher has been used, When I try to change DiscountValue, Then I see the same error
- [ ] Given a voucher has been used, When I try to change MinOrderValue, Then I see the same error
- [ ] Given ExpiryDate, When I try to extend it after first use, Then I see "Expiry can only be shortened after first usage"
- [ ] Given I shorten the ExpiryDate, Then the change is accepted

### Business Rules

- Locked fields after first VoucherUsage: DiscountType, DiscountValue, MinOrderValue
- ExpiryDate: can be shortened but NOT extended after first use
- Other fields (UsageLimit, UsageLimitPerUser): can still be modified
- Invariant V6: domain guard checks `Usages.Any()`

### Technical Notes

- Domain guard: `if (Usages.Any()) throw` on modification of locked fields
- Checked in `Update()` domain method
- Invariant V6

### Priority

Phase 1

