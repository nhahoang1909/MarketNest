# Admin Module — Functional Specification

> Module: `MarketNest.Admin` | Schema: `admin` | Version: 1.0 | Date: 2026-05-01

## Module Overview

The Admin module provides back-office management for platform operators. It covers storefront/product moderation, commission and payment configuration, user management, prohibited categories, voucher oversight, and platform-wide announcements.

## Actors

| Actor | Relevant Actions |
|-------|-----------------|
| Admin | All actions in this module |

---

## US-ADMIN-001: Suspend Storefront

**As an** admin, **I want to** suspend a storefront with a reason, **so that** policy-violating sellers are removed from public view.

### Acceptance Criteria

- [ ] Given a storefront is Active, When I suspend it with a reason, Then status changes to `Suspended`
- [ ] Given a storefront is suspended, Then all its products are hidden from public browse/search
- [ ] Given a storefront is suspended, Then the seller is notified with the reason
- [ ] Given I provide no reason, When I try to suspend, Then I see "Reason is required"
- [ ] Given a storefront is already suspended, Then the suspend button is not shown

### Business Rules

- Reason is mandatory (free text, stored for audit)
- Suspended storefront: all products hidden from public view
- Existing orders remain unaffected (fulfillment continues)
- Seller receives notification with reason
- Admin can un-suspend (reactivate) later

### Technical Notes

- Domain event: `StorefrontSuspendedEvent(reason)` → Notifications (seller)
- Status transition: Active → Suspended
- Audit: `[Audited("STOREFRONT_SUSPENDED")]`

### Priority

Phase 1

---

## US-ADMIN-002: Suspend Product

**As an** admin, **I want to** suspend an individual product with a reason, **so that** specific policy-violating listings are removed.

### Acceptance Criteria

- [ ] Given a product is Active, When I suspend it with a reason, Then it's hidden from public view
- [ ] Given a product is suspended, Then it no longer appears in search/browse
- [ ] Given the product is in buyers' carts, Then cart shows "Product unavailable" on next view
- [ ] Given the seller views their dashboard, Then the product shows as "Admin Suspended" with reason

### Business Rules

- Reason mandatory
- Product hidden from public (not deleted)
- Active orders containing this product: unaffected
- Seller can see the suspension reason in their dashboard

### Technical Notes

- Similar to storefront suspension but at product level
- May reuse `ProductStatus` or add a separate `AdminSuspended` flag
- Audit: `[Audited("PRODUCT_SUSPENDED")]`

### Priority

Phase 1

---

## US-ADMIN-003: Configure Commission Rate

**As an** admin, **I want to** set the commission rate per seller, **so that** the platform earns revenue on each sale.

### Acceptance Criteria

- [ ] Given I update a seller's commission rate (e.g., from 10% to 12%), When saved, Then future orders use the new rate
- [ ] Given I don't set a custom rate for a seller, Then the default rate (10%) applies
- [ ] Given I change the rate, Then existing orders are unaffected (they use the snapshotted rate)
- [ ] Given the rate is outside valid range (0–50%), Then I see validation error

### Business Rules

- Default commission rate: 10%
- Rate configurable per seller (stored on Storefront.CommissionRate)
- Rate changes only affect orders placed AFTER the change date
- Existing orders use `CommissionRateSnapshot` (captured at order time)
- Valid range: 0%–50%
- Invariant F5: snapshot at order placed time

### Technical Notes

- Field: `Storefront.CommissionRate`
- Admin endpoint: `PATCH api/v1/admin/storefronts/{id}/commission`
- Audit: `[Audited("COMMISSION_RATE_CHANGED")]`

### Priority

Phase 1

---

## US-ADMIN-004: Configure Payment Surcharge Rate

**As an** admin, **I want to** configure the payment surcharge rate per payment method, **so that** gateway costs are transparently passed to buyers.

### Acceptance Criteria

- [ ] Given I set CreditCard surcharge to 2%, When saved, Then checkouts using CreditCard show 2% surcharge
- [ ] Given I set BankTransfer surcharge to 0%, Then no surcharge line appears at checkout
- [ ] Given I change the rate, Then it applies to new checkouts (existing orders keep snapshot)
- [ ] Given rate is outside valid range (0–10%), Then I see validation error

### Business Rules

- Surcharge rate: per PaymentMethod
- Phase 1 methods: CreditCard (default 2%), BankTransfer (default 0%)
- Rate snapshotted on Order at checkout time
- Surcharge displayed as separate line item (transparency requirement)
- Invariant F4: snapshot at checkout

### Technical Notes

- Master data table: PaymentMethodConfig (method, surchargeRate)
- Admin endpoint for configuration
- See §10.3 PaymentSurcharge Configuration

### Priority

Phase 1

---

## US-ADMIN-005: Ban User

**As an** admin, **I want to** ban a user from the platform, **so that** bad actors are removed.

### Acceptance Criteria

- [ ] Given I ban a user, When saved, Then their account is flagged as banned
- [ ] Given a banned user tries to login, Then they see "Account suspended — contact support"
- [ ] Given a banned seller, Then their storefront is automatically suspended
- [ ] Given I ban a user, Then all their refresh tokens are revoked (forced logout)

### Business Rules

- Banned users cannot login
- Banned sellers: storefront auto-suspended
- Existing orders: continue to completion (fulfillment not blocked)
- All refresh tokens revoked on ban (immediate session kill)
- Ban reason stored for audit

### Technical Notes

- Cross-module: Identity (ban flag, token revocation), Catalog (storefront suspension)
- May use domain event: `UserBannedEvent` → handlers in relevant modules
- Audit: `[Audited("USER_BANNED")]`

### Priority

Phase 1

---

## US-ADMIN-006: Manage Prohibited Categories

**As an** admin, **I want to** maintain a list of prohibited product categories, **so that** sellers cannot list items in restricted categories.

### Acceptance Criteria

- [ ] Given I add a category to the prohibited list, When saved, Then sellers cannot create products in that category
- [ ] Given a seller tries to publish a product in a prohibited category, Then they see "This category is not allowed"
- [ ] Given I remove a category from the prohibited list, Then it becomes available for product listing
- [ ] Given existing products are in a newly prohibited category, Then admin is notified to review them

### Business Rules

- Prohibited categories: configurable list (admin-managed reference data)
- Check enforced at product publish time (not at draft creation)
- Existing products in newly prohibited categories: flagged for admin review (not auto-removed)

### Technical Notes

- ReferenceData entity: `ProhibitedCategory` (code + display name)
- Check at product publish command handler
- Seeded as part of admin data seeders

### Priority

Phase 1

---

## US-ADMIN-007: Pause Any Voucher

**As an** admin, **I want to** pause any voucher (platform or shop), **so that** problematic promotions can be stopped immediately.

### Acceptance Criteria

- [ ] Given a voucher is Active, When I pause it, Then status changes to `Paused`
- [ ] Given a paused voucher, Then it cannot be applied at checkout
- [ ] Given I pause a shop voucher, Then the seller is notified
- [ ] Given a paused voucher, Then admin can re-activate it later

### Business Rules

- Admin can pause both Platform and Shop vouchers
- Paused vouchers: not validatable at checkout
- Seller notified when their shop voucher is paused by admin
- Status transition: Active → Paused (admin); Paused → Active (admin or seller)
- `VoucherPausedEvent` → Notifications (seller)

### Technical Notes

- Domain event: `VoucherPausedEvent` → Notifications (if shop voucher)
- Cross-module: Admin endpoint calls Promotions module
- Template: notification to seller if their voucher is paused

### Priority

Phase 1

---

## US-ADMIN-008: Force-Remove Variant Sale Price

**As an** admin, **I want to** force-remove a sale price from any variant, **so that** I can correct pricing issues or policy violations.

### Acceptance Criteria

- [ ] Given a variant has an active sale, When I force-remove it, Then SalePrice/SaleStart/SaleEnd are cleared
- [ ] Given removal succeeds, Then `VariantSalePriceRemovedEvent` is raised
- [ ] Given removal succeeds, Then the seller is notified with reason

### Business Rules

- Admin can remove sale price from any variant regardless of ownership
- Reason should be logged for audit
- Same domain logic as seller removal (`RemoveSalePrice()`)

### Technical Notes

- Admin endpoint: `DELETE api/v1/admin/catalog/variants/{id}/sale`
- Domain event: `VariantSalePriceRemovedEvent`
- Audit: `[Audited("ADMIN_SALE_REMOVED")]`

### Priority

Phase 1

---

## US-ADMIN-009: Create/Publish/Unpublish Announcements

**As an** admin, **I want to** create and schedule platform-wide announcements, **so that** all users see important messages.

### Acceptance Criteria

- [ ] Given I create an announcement with title, message, type, and date range, Then it's saved in draft (unpublished)
- [ ] Given I publish the announcement, When the start date arrives, Then it appears as a banner on all public pages
- [ ] Given the announcement has a link (URL + text), Then a CTA button is shown in the banner
- [ ] Given the end date passes, Then the banner automatically disappears
- [ ] Given I unpublish an announcement, Then it immediately stops showing
- [ ] Given I set `IsDismissible = true`, Then users can close the banner (dismiss stored in localStorage)
- [ ] Given multiple active announcements, Then they stack ordered by SortOrder DESC

### Business Rules

- Types: Info (blue), Promotion (green), Warning (amber), Urgent (red)
- Scheduling: `StartDateUtc < EndDateUtc`; `IsActive()` = IsPublished && within date range
- Dismissible banners: dismiss state in localStorage (`mn-dismiss-{id}`)
- Sort: SortOrder DESC, StartDateUtc DESC
- No per-user targeting (Phase 1)
- No impression tracking (Phase 1)

### Technical Notes

- Entity: `Announcement` in admin schema (not Aggregate Root — simple entity)
- DB index: `IX_Announcements_Active` on (IsPublished, StartDateUtc, EndDateUtc)
- HTMX lazy-load: `/Shared/AnnouncementBanner`
- ADR-043
- Domain methods: `Publish()`, `Unpublish()`, `Update(…)`, `IsActive(utcNow)`

### Priority

Phase 1

---

## US-ADMIN-010: Arbitrate Disputes

**As an** admin, **I want to** review and resolve disputes between buyers and sellers, **so that** conflicts are fairly adjudicated.

### Acceptance Criteria

- [ ] Given a dispute is `UnderReview`, When I view it, Then I see the full message thread with evidence from both parties
- [ ] Given I make a decision (FullRefund/PartialRefund/Dismiss), When I resolve it, Then the appropriate payment and order actions are triggered
- [ ] Given I resolve with PartialRefund, When I specify an amount, Then exactly that amount is refunded
- [ ] Given I provide an admin note, Then it's stored as part of the resolution record
- [ ] Given dispute is resolved, Then both buyer and seller are notified of the outcome

### Business Rules

- Same as US-DISPUTE-005 (Admin Reviews and Resolves)
- Admin note required
- Resolution is final (Phase 1)
- Triggers: payment action + order status change + notifications

### Technical Notes

- Reuses Disputes module's resolution logic
- Admin UI: dedicated dispute review page with message thread
- Cross-module: triggers via `DisputeResolvedEvent`

### Priority

Phase 1

---

## US-ADMIN-011: Platform Dashboard Overview

**As an** admin, **I want to** see a dashboard with key platform metrics, **so that** I can monitor marketplace health.

### Acceptance Criteria

- [ ] Given I access the admin dashboard, Then I see: total orders (today/week/month), total revenue, active disputes, pending payouts
- [ ] Given there are pending actions (unresolved disputes, pending payouts), Then they're highlighted
- [ ] Given I want to drill down, Then each metric links to the relevant detailed page

### Business Rules

- Dashboard shows real-time aggregated metrics
- Key metrics: orders count, revenue, active sellers, disputes pending, payouts pending
- Quick action links to: disputes queue, payout queue, flagged content

### Technical Notes

- Query: `GetAdminDashboardQuery` (aggregates across modules via service contracts)
- UI: uses chart.js for visualizations
- HTMX: lazy-loaded stat cards

### Priority

Phase 1

