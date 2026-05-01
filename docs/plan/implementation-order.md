# Implementation Order — MarketNest Phase 1

> Last updated: 2026-05-01
> Total use cases: 104 across 10 modules
> Sequenced by dependency graph. Each wave can only start after the previous wave is complete.

## Dependency Graph (summary)

```
Identity (auth) → RBAC/Admin → Seller Onboarding
                                    ↓
                              Catalog (storefront → product → inventory → pricing)
                                    ↓
                         Cart (add → reserve → checkout)
                                    ↓
                    ┌──────────────────────────────┐
                    ↓                              ↓
              Promotions                     Notifications
              (voucher validation)           (event handlers)
                    ↓
                 Orders (state machine)
                    ↓
                Payments (capture → payout)
                    ↓
                Disputes (post-delivery)
                    ↓
                 Reviews (post-completion)
```

---

## Wave 1 — Core Auth (blocker for everything)

> Without login/register nothing else works. No dependencies.

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 1 | US-IDENT-001: Buyer Registration | #47 | Entry point for all users |
| 2 | US-IDENT-003: Email Verification (OTP) | #49 | Required before seller apply & storefront activate |
| 3 | US-IDENT-004: Login (JWT + Refresh Token) | #50 | All protected routes depend on this |
| 4 | US-IDENT-004a: Token Refresh | #139 | Session continuity; needed by RBAC permission refresh |
| 5 | US-IDENT-005: Password Reset | #51 | Security baseline |
| 6 | US-IDENT-011: Change Password | #57 | Security baseline |

---

## Wave 2 — RBAC Foundation (blocker for admin ops)

> Roles and permissions must exist before admin can do anything meaningful.
> Depends on: Wave 1 (admin must be able to log in).

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 7 | US-ADMIN-005d: Manage Role Permissions | #138 | Set up role–permission matrix first |
| 8 | US-ADMIN-005a: Assign / Revoke Roles | #135 | Depends on role permission matrix |
| 9 | US-ADMIN-005b: Manage User Permission Overrides | #136 | Fine-grained overrides on top of roles |
| 10 | US-ADMIN-005: Suspend / Reinstate User | #40 | Requires RBAC `UserPermission.Suspend` |

---

## Wave 3 — Seller Onboarding Pipeline

> Seller role must exist (Wave 2) before an application can grant it.
> Depends on: Wave 1 (email verified), Wave 2 (admin can approve).

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 11 | US-IDENT-002: Seller Application | #48 | Buyer submits; triggers admin queue |
| 12 | US-ADMIN-005c: Review Seller Applications | #137 | Admin approves → Seller role + Storefront draft created |

---

## Wave 4 — User Profile & Preferences

> Profile data is read by Catalog (seller bio), Notifications (timezone, email target), and Orders (address).
> Depends on: Wave 1 (must be logged in).

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 13 | US-IDENT-006: Profile Update | #52 | Avatar upload, bio (seller only), phone |
| 14 | US-IDENT-007: Manage Addresses | #53 | Shipping address required at checkout |
| 15 | US-IDENT-008: User Preferences (Timezone, Format, Language) | #54 | Used by Notifications digest scheduling |
| 16 | US-IDENT-009: Notification Preferences | #55 | Toggle channels; needed before Notifications wave |
| 17 | US-IDENT-010: Privacy Settings | #56 | Controls storefront visibility in browse |

---

## Wave 5 — Catalog: Storefront & Core Products

> The marketplace has nothing to sell until this wave is done.
> Depends on: Wave 3 (seller role), Wave 2 (admin can suspend storefront/set commission).

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 18 | US-ADMIN-003: Configure Commission Rate | #38 | Must exist before orders snapshot rate |
| 19 | US-ADMIN-004: Configure Payment Surcharge Rate | #39 | Must exist before checkout calculates surcharge |
| 20 | US-ADMIN-006: Manage Prohibited Categories | #41 | Checked at product publish time |
| 21 | US-CATALOG-001: Create Storefront | #67 | First step for every seller |
| 22 | US-CATALOG-002: Activate Storefront | #68 | Slug immutable after this; products can be published |
| 23 | US-CATALOG-003: Create Product with Variants | #69 | Core listing; SKU, price, stock |
| 24 | US-CATALOG-006: Update Product Details | #72 | Edit title, description, tags, category |
| 25 | US-CATALOG-007: Manage Variant Inventory | #73 | Stock levels; needed before cart reservation |
| 26 | US-CATALOG-004: Publish Product | #70 | Makes product visible; triggers search index |
| 27 | US-CATALOG-005: Archive Product | #71 | Reverse of publish |
| 28 | US-CATALOG-010: Browse/Search Active Products | #76 | Public-facing product discovery |
| 29 | US-CATALOG-011: View Storefront Page | #77 | Public storefront page |
| 30 | US-ADMIN-001: Suspend Storefront | #35 | Moderation; hides all products |
| 31 | US-ADMIN-002: Suspend Product | #36 | Fine-grained moderation |

---

## Wave 6 — Catalog: Pricing & Advanced Features

> Depends on: Wave 5 (products exist and have inventory).

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 32 | US-CATALOG-008: Set Sale Price on Variant | #74 | EffectivePrice() must be ready before Cart |
| 33 | US-CATALOG-009: Remove Sale Price | #75 | Paired with set sale price |
| 34 | US-CATALOG-014: Expire Sales Background Job | #80 | Automated cleanup; depends on sale fields existing |
| 35 | US-ADMIN-008: Force-Remove Variant Sale Price | #43 | Admin override of seller sale price |
| 36 | US-CATALOG-013: Low Inventory Alert | #79 | Raises domain events; depends on Notifications (Wave 9) for delivery |
| 37 | US-CATALOG-012: Follow/Unfollow Storefront | #78 | Nice-to-have; no blocker downstream |
| 38 | US-CATALOG-015: Bulk Import Variants via Excel | #81 | Seller productivity; no blocker downstream |

---

## Wave 7 — Announcements & Admin Config UX

> Can be built any time after Wave 2 (admin login). No hard dependency on later waves.

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 39 | US-ADMIN-009: Create/Publish/Unpublish Announcements | #44 | Platform banners; ADR-043 already scaffolded |

---

## Wave 8 — Cart

> Cart is the bridge between Catalog and Orders.
> Depends on: Wave 5–6 (products, EffectivePrice(), inventory).

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 40 | US-CART-001: Add Item to Cart | #59 | Redis reservation; EffectivePrice() snapshot |
| 41 | US-CART-002: Update Item Quantity | #60 | Adjust reservation delta |
| 42 | US-CART-003: Remove Item from Cart | #61 | Release reservation |
| 43 | US-CART-004: View Cart with Price Drift Detection | #62 | Live price vs snapshot; cross-module Catalog read |
| 44 | US-CART-005: Reservation TTL Refresh (Heartbeat) | #63 | Redis EXPIRE; client-side heartbeat |
| 45 | US-CART-006: Reservation Release on TTL Expiry | #64 | Background job `cart.reservation.cleanup` |
| 46 | US-CART-007: Wishlist (Add/Remove/View) | #65 | No reservation; informational only |
| 47 | US-IDENT-012: Guest Cart Merge on Login | #58 | Requires Cart (items) + Identity (login event) |
| 48 | US-CART-008: Cart Checkout Initiation | #66 | Status → CheckedOut; raises CartCheckedOutEvent |

---

## Wave 9 — Notifications Core

> Many later waves emit domain events that Notifications must handle.
> Build notification dispatch infrastructure before Orders/Disputes/Reviews to avoid stub handlers.
> Depends on: Wave 4 (user preferences / toggle settings).

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 49 | US-NOTIF-001: Send Notification on Domain Event | #89 | Core dispatch pipeline; template engine |
| 50 | US-NOTIF-002: Security Notifications Bypass Preferences | #90 | Must be done alongside auth (Wave 1 can use stubs) |
| 51 | US-NOTIF-003: Respect User Notification Toggles | #91 | Reads NotificationPreference (Wave 4) |
| 52 | US-NOTIF-005: In-App Notification Inbox | #93 | UI bell + unread count |

---

## Wave 10 — Promotions / Vouchers

> Vouchers must be validated before checkout calculates BuyerTotal.
> Depends on: Wave 5 (Catalog/prices), Wave 8 (Cart checkout flow).

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 53 | US-PROMO-001: Admin Creates Platform Voucher | #118 | Admin creates voucher entity |
| 54 | US-PROMO-002: Seller Creates Shop Voucher | #119 | Seller creates voucher for own store |
| 55 | US-PROMO-003: Activate Voucher | #120 | Draft → Active |
| 56 | US-PROMO-004: Pause Voucher | #121 | Active ↔ Paused |
| 57 | US-ADMIN-007: Pause Any Voucher | #42 | Admin override of any voucher |
| 58 | US-PROMO-010: Voucher Immutability After First Usage | #127 | Domain guard; must be in place before first use |
| 59 | US-PROMO-005: Apply Voucher at Checkout | #122 | Core validation sequence; discount calculation |
| 60 | US-PROMO-006: Voucher Stacking Rules | #123 | Enforced during checkout |
| 61 | US-PROMO-007: Per-User Usage Limit | #124 | Part of validation sequence |
| 62 | US-PROMO-008: Auto-Expire/Deplete Background Job | #125 | `promotions.voucher.expiry` hourly job |

---

## Wave 11 — Orders + Payments (critical path core)

> The full purchase flow. This is the Phase 1 exit-criteria backbone.
> Depends on: Wave 8 (CartCheckedOutEvent), Wave 9 (notifications), Wave 10 (voucher discount applied to BuyerTotal).

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 63 | US-PAY-008: Payment Surcharge Display at Checkout | #116 | UI must show surcharge before order is placed |
| 64 | US-ORDER-001: Place Order from Cart | #95 | Creates Order + financial snapshot |
| 65 | US-PAY-001: Capture Payment on Checkout | #109 | Mock gateway; raises PaymentCapturedEvent |
| 66 | US-ORDER-002: Payment Confirmed → CONFIRMED | #96 | Listens for PaymentCapturedEvent |
| 67 | US-ORDER-014: Order State Machine Enforcement | #108 | Domain guards; must be solid before adding transitions |
| 68 | US-ORDER-003: Seller Confirms → PROCESSING | #97 | Seller action; resets 48h timer |
| 69 | US-ORDER-004: Seller Ships → SHIPPED | #98 | Tracking required; starts 30-day auto-deliver timer |
| 70 | US-ORDER-005: Buyer Confirms Delivery → DELIVERED | #99 | Starts 3-day dispute window |
| 71 | US-ORDER-011: View Order Details | #105 | Buyer and seller financial breakdown |

---

## Wave 12 — Orders: Cancellation & Auto-Jobs

> Cancellation needs refund logic (Payments). Auto-jobs extend the state machine.
> Depends on: Wave 11 (Order exists in various states).

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 72 | US-PAY-002: Payment Failure Handling | #110 | Order stays Pending on failure; retry flow |
| 73 | US-ORDER-008: Seller Cancels Order | #102 | Confirmed / Processing only |
| 74 | US-ORDER-009: Buyer Cancels Order | #103 | Confirmed only |
| 75 | US-PAY-003: Full Refund on Cancellation | #111 | Listens for OrderCancelledEvent |
| 76 | US-PROMO-009: Reverse Usage on Order Cancel/Refund | #126 | Decrement voucher UsageCount on cancel |
| 77 | US-ORDER-010: Auto-Cancel Unconfirmed After 48h | #104 | Background job every 30 min |
| 78 | US-ORDER-006: Auto-Delivery After 30 Days Shipped | #100 | Daily job 01:00 UTC |
| 79 | US-ORDER-007: Auto-Complete After 3 Days Delivered | #101 | Daily job 01:05 UTC; raises OrderCompletedEvent |
| 80 | US-ORDER-012: Shipping Preference Warning at Checkout | #106 | Informational; non-blocking |
| 81 | US-ORDER-013: Order Preferences | #107 | User settings for order notifications |

---

## Wave 13 — Payments: Payouts

> Payouts only trigger after OrderCompleted (Wave 12 auto-complete job).
> Depends on: Wave 11–12 (Order lifecycle complete).

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 82 | US-PAY-005: Schedule Payout on Order COMPLETED | #113 | Listens for OrderCompletedEvent; creates Payout |
| 83 | US-PAY-006: Process Payout Batch | #114 | Daily job 02:00 UTC; mock disbursement Phase 1 |
| 84 | US-PAY-009: Seller Payout History | #117 | Seller dashboard; paged query |

---

## Wave 14 — Disputes

> Disputes open after delivery (Delivered status). Requires Order, Payment, and Notifications.
> Depends on: Wave 11 (Delivered state), Wave 12 (refund logic), Wave 9 (notifications).

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 85 | US-DISPUTE-001: Open Dispute | #82 | Within 3 days of DeliveredAt |
| 86 | US-DISPUTE-002: Submit Evidence | #83 | Append-only DisputeMessage |
| 87 | US-DISPUTE-007: Immutable Message Audit Trail | #88 | Enforce no-update on dispute_messages |
| 88 | US-DISPUTE-003: Seller Response Within 72h Deadline | #84 | Moves dispute to UnderReview |
| 89 | US-DISPUTE-004: Auto-Escalate on Seller Timeout | #85 | Background job or deadline check |
| 90 | US-DISPUTE-005: Admin Reviews and Resolves | #86 | Raises DisputeResolvedEvent |
| 91 | US-ADMIN-010: Arbitrate Disputes | #45 | Admin UI page for dispute management |
| 92 | US-DISPUTE-006: Resolution Triggers Payment Action | #87 | Cross-module: Payments refund + Orders status |
| 93 | US-PAY-004: Partial Refund After Dispute Resolution | #112 | Listens for DisputeResolvedEvent |
| 94 | US-PAY-007: Payout Clawback | #115 | Payout already paid → clawback required |

---

## Wave 15 — Reviews

> Reviews require a COMPLETED order (no disputed/refunded orders allowed).
> Depends on: Wave 12 (OrderCompleted), Wave 14 (dispute blocking logic).

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 95 | US-REVIEW-007: Block Review on Disputed/Refunded Orders | #134 | Gate logic; implement first to prevent bypasses |
| 96 | US-REVIEW-001: Submit Review | #128 | Review gate + rating (1–5) + domain event |
| 97 | US-REVIEW-006: Product Rating Aggregation | #133 | Event-driven recalculation; updates Product.AverageRating |
| 98 | US-REVIEW-002: Edit Review Within 24 Hours | #129 | IsEditable computed from CreatedAt + 24h |
| 99 | US-REVIEW-003: Seller Reply to Review | #130 | One reply per review; ownership check |
| 100 | US-REVIEW-004: Vote on Review | #131 | Helpful vote; unique per buyer per review |
| 101 | US-REVIEW-005: Hide/Flag Review (Admin) | #132 | Triggers rating recalculation |

---

## Wave 16 — Notifications: Advanced Features

> Digest and alternate email are non-critical path; can be deferred to end of Phase 1.
> Depends on: Wave 9 (notification core infrastructure).

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 102 | US-NOTIF-004: Daily Digest Batching | #92 | `notifications.digest.daily` job; needs UserPreferences.Timezone |
| 103 | US-NOTIF-006: Alternate Email Delivery | #94 | Alternate email verification + routing |

---

## Wave 17 — Admin Dashboard

> Dashboard aggregates data across all modules; only meaningful when there is real data to show.
> Depends on: Waves 11–14 (orders, payments, disputes all exist).

| # | Use Case | GitHub Issue | Notes |
|---|----------|-------------|-------|
| 104 | US-ADMIN-011: Platform Dashboard Overview | #46 | Aggregated metrics via GetAdminDashboardQuery |

---

## Summary by Wave

| Wave | Focus | Use Cases | Issues |
|------|-------|-----------|--------|
| 1 | Core Auth | 6 | #47, #49, #50, #139, #51, #57 |
| 2 | RBAC Foundation | 4 | #138, #135, #136, #40 |
| 3 | Seller Onboarding | 2 | #48, #137 |
| 4 | User Profile & Preferences | 5 | #52–#56 |
| 5 | Catalog: Storefront & Products | 14 | #38–#39, #41, #67–#70, #72–#73, #76–#77, #35–#36 |
| 6 | Catalog: Pricing & Advanced | 7 | #74–#75, #80, #43, #79, #78, #81 |
| 7 | Announcements | 1 | #44 |
| 8 | Cart | 9 | #59–#66, #58 |
| 9 | Notifications Core | 4 | #89–#91, #93 |
| 10 | Promotions / Vouchers | 10 | #118–#125, #42, #127 |
| 11 | Orders + Payments (core) | 9 | #116, #95–#99, #109, #96, #108, #105 |
| 12 | Orders: Cancellation & Auto-Jobs | 10 | #110, #102–#104, #111, #126, #100–#101, #106–#107 |
| 13 | Payments: Payouts | 3 | #113–#114, #117 |
| 14 | Disputes | 10 | #82–#88, #45, #112, #115 |
| 15 | Reviews | 7 | #128–#134 |
| 16 | Notifications: Advanced | 2 | #92, #94 |
| 17 | Admin Dashboard | 1 | #46 |
| **Total** | | **104** | |

---

## Phase 1 Critical Path (minimum viable product)

The minimum set to satisfy the Phase 1 exit criterion
("browse → register → create storefront → list product → another user buys it → order fulfilled"):

```
Wave 1 (auth) → Wave 2 (RBAC) → Wave 3 (seller onboarding)
    → Wave 5 (storefront + product) → Wave 8 (cart)
    → Wave 11 (order + payment capture → confirmed → shipped → delivered)
    → Wave 12 (auto-complete) → Wave 13 (payout)
```

Everything else (disputes, reviews, promotions, notifications, dashboard) is layered on top of this core.
