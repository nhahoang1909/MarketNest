# Admin Module — Functional Specification

> Module: `MarketNest.Admin` | Schema: `admin` | Version: 2.0 | Date: 2026-05-01
> Updated: ADR-044 — RBAC user/role/permission management use cases added.

## Module Overview

The Admin module provides back-office management for platform operators. It covers storefront/product moderation, commission and payment configuration, user management (roles, permissions, suspend/reinstate), seller application review, prohibited categories, voucher oversight, and platform-wide announcements.

## Actors

| Actor | Relevant Actions |
|-------|-----------------|
| Administrator | All actions in this module (requires appropriate `UserPermission` / `ConfigPermission` flags) |

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

## US-ADMIN-005: Suspend / Reinstate User

**As an** admin, **I want to** suspend or reinstate a user account, **so that** bad actors are removed and recovered users regain access.

### Acceptance Criteria

- [ ] Given I suspend a user with a reason, When saved, Then their status changes to `Suspended`
- [ ] Given a suspended user tries to login, Then they see "Account suspended — contact support"
- [ ] Given a suspended seller, Then their storefront is automatically suspended (via domain event)
- [ ] Given I suspend a user, Then all their refresh tokens are revoked (forced logout)
- [ ] Given I reinstate a suspended user, Then their status returns to `Active` and they can login again
- [ ] Given I reinstate a suspended seller, Then their storefront is NOT auto-reactivated (manual step)

### Business Rules

- Suspended users cannot login
- Suspended sellers: storefront auto-suspended via `UserSuspendedEvent`
- Existing orders: continue to completion (fulfillment not blocked)
- All refresh tokens revoked on suspend (immediate session kill)
- Suspension reason stored for audit (mandatory)
- Reinstatement does not auto-reactivate storefront — seller must request reactivation
- Permission required: `UserPermission.Suspend`

### Technical Notes

- Domain events: `UserSuspendedEvent` → Catalog (storefront), Notifications (user)
- Domain event: `UserReinstatedEvent` → audit log
- Audit: `[Audited("USER_SUSPENDED")]` / `[Audited("USER_REINSTATED")]`

### Priority

Phase 1

---

## US-ADMIN-005a: Assign / Revoke Roles

**As an** admin, **I want to** assign or revoke roles from users, **so that** I can control their platform access level.

### Acceptance Criteria

- [ ] Given I assign the `Administrator` role to a user, When saved, Then they gain full admin permissions on next login
- [ ] Given I try to assign `SystemAdmin` role, Then I see an error "SystemAdmin cannot be assigned via UI"
- [ ] Given I try to assign `Seller` role directly (without approved application), Then I see an error "Seller role requires approved application"
- [ ] Given I revoke `Seller` role from a user, Then all their active products are archived (domain event)
- [ ] Given the user already has the role, When I try to assign it again, Then I see "Role already assigned"
- [ ] Given I revoke a role, Then the user's next JWT reflects reduced permissions

### Business Rules

- `SystemAdmin` cannot be assigned via any endpoint
- `Administrator` can only be granted by another Administrator (`UserPermission.Manage`)
- `Seller` is normally assigned via seller application approval — manual grant is a fallback for admin override
- Revoking `Seller` archives all active products (via `RoleRevokedEvent` → handler)
- Role changes take effect on next login/refresh (JWT re-issued)

### Technical Notes

- Commands: `AssignRoleCommand(UserId, RoleId, AdminId)`, `RevokeRoleCommand(UserId, RoleId, AdminId)`
- Domain events: `RoleAssignedEvent`, `RoleRevokedEvent`
- Endpoint: `POST /admin/users/{id}/roles/assign`, `DELETE /admin/users/{id}/roles/{roleId}`
- Permission required: `UserPermission.Manage`

### Priority

Phase 1

---

## US-ADMIN-005b: Manage User Permission Overrides

**As an** admin, **I want to** grant or deny specific permissions to individual users beyond their role defaults, **so that** I can fine-tune access without creating new roles.

### Acceptance Criteria

- [ ] Given I grant `Refund` permission to a seller (normally admin-only), When saved, Then that seller can process refunds on next login
- [ ] Given I deny `Publish` permission from a seller, When saved, Then they cannot publish products even though their Seller role normally grants it
- [ ] Given I set an expiry date on an override, When the date passes, Then the override is ignored at next token refresh
- [ ] Given I clear all overrides for a user/module, When saved, Then they revert to pure role-based permissions
- [ ] Given I view a user's effective permissions, Then I see the final computed permissions (roles + overrides)

### Business Rules

- Overrides are per-module per-user (one row per user per `PermissionModule`)
- `GrantedFlags` are OR'd into effective permissions (add capabilities)
- `DeniedFlags` are cleared from effective permissions (remove capabilities)
- Overrides support optional expiry (`ExpiresAt`) — expired overrides ignored
- Only `UserPermission.Manage` holders can set overrides
- Admin can manage ALL resource permissions for ALL users

### Technical Notes

- Command: `SetUserPermissionOverrideCommand(UserId, Module, GrantedFlags, DeniedFlags, AdminId)`
- Endpoint: `PUT /admin/users/{id}/permissions`
- Domain method: `user.GrantPermissionOverride(request, adminId)` — upserts per module
- Effect visible on next login/refresh (JWT re-computed)
- Admin UI: checkbox grid per module showing current effective permissions

### Priority

Phase 1

---

## US-ADMIN-005c: Review Seller Applications

**As an** admin, **I want to** review and approve or reject seller applications, **so that** only qualified sellers can sell on the platform.

### Acceptance Criteria

- [ ] Given there are pending applications, When I view the queue, Then I see them sorted by submission date
- [ ] Given I view an application, Then I see business name, documents, tax ID, and applicant info
- [ ] Given I approve an application with an optional note, Then the applicant receives Seller role and a Storefront draft is created
- [ ] Given I reject an application with a mandatory reason, Then the applicant is notified with the reason
- [ ] Given an application is already approved/rejected, Then I cannot change its status

### Business Rules

- Approval: `SellerApplicationApprovedEvent` → assigns Seller role + creates Storefront draft
- Rejection: `SellerApplicationRejectedEvent` → notification to applicant
- Admin review note stored for audit
- Applications in `Approved` or `Rejected` status cannot be re-reviewed
- Permission required: `UserPermission.Manage`

### Technical Notes

- Commands: `ApproveSellerApplicationCommand`, `RejectSellerApplicationCommand`
- Endpoints: `POST /admin/seller-applications/{id}/approve`, `POST /admin/seller-applications/{id}/reject`
- Page: `/admin/seller-applications` (list + detail)
- Domain event handler `AssignSellerRoleHandler` handles the role + storefront creation

### Priority

Phase 1

---

## US-ADMIN-005d: Manage Role Permissions

**As an** admin, **I want to** view and modify the permission flags assigned to each role, **so that** I can adjust platform access without code changes.

### Acceptance Criteria

- [ ] Given I view a role's detail page, Then I see a matrix of module × permissions with current flags
- [ ] Given I toggle a permission flag for a role (e.g., add `Export` to Seller's Order permissions), When saved, Then the role's flags are updated
- [ ] Given a system role (`IsSystem = true`), Then I cannot delete it but can modify its permissions
- [ ] Given I update a role's permissions, Then all users with that role get updated permissions on next login

### Business Rules

- Permission flags are code-defined (`[Flags]` enums) — admin cannot invent new permissions at runtime
- Admin can only adjust which flags are assigned to which roles
- Changes take effect on next JWT issuance (login or refresh) for affected users
- System roles cannot be deleted
- Permission required: `ConfigPermission.Write`

### Technical Notes

- Command: `UpdateRolePermissionsCommand(RoleId, Module, Flags, AdminId)`
- Endpoint: `PUT /admin/roles/{id}/permissions`
- Page: `/admin/roles` (list), `/admin/roles/{id}` (edit)
- UI: checkbox grid with module rows × action columns

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

