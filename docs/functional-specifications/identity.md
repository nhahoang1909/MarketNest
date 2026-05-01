# Identity Module — Functional Specification

> Module: `MarketNest.Identity` | Schema: `identity` | Version: 2.0 | Date: 2026-05-01
> Updated: ADR-044 — RBAC system with multi-role, bitwise permissions, per-user overrides, seller application flow.

## Module Overview

The Identity module handles user registration, authentication, profile management, role/permission management, and the seller onboarding pipeline. It is the central authority for user accounts, multi-role assignments (SystemAdmin, Administrator, Seller, Buyer), bitwise permission flags (per module), per-user permission overrides, and cross-module user identity contracts.

## Actors

| Actor | Relevant Actions |
|-------|-----------------|
| Guest | Register, login, password reset |
| Buyer | Manage profile, addresses, preferences, privacy settings, apply to become seller |
| Seller | All Buyer actions + public bio, storefront activation prerequisite |
| Administrator | Manage users, assign/revoke roles, grant/deny permissions, approve/reject seller applications |
| SystemAdmin | Background jobs, migrations — never a real login (audit trail identity only) |

---

## US-IDENT-001: Buyer Registration

**As a** guest, **I want to** register as a buyer with email and password, **so that** I can place orders on the platform.

### Acceptance Criteria

- [ ] Given I am on the registration page, When I submit a valid email, password, and display name, Then my account is created with the `Buyer` role
- [ ] Given I submit an email that already exists, When the form is submitted, Then I see an error "Email already registered"
- [ ] Given I submit a password shorter than the minimum length, When the form is submitted, Then I see a validation error
- [ ] Given registration succeeds, When my account is created, Then a verification email is sent to my address
- [ ] Given registration succeeds, Then my `UserPreferences`, `NotificationPreference`, and `UserPrivacy` entities are created with default values

### Business Rules

- Email must be unique (case-insensitive)
- Password minimum length: `AppConstants.Validation.PasswordMinLength`
- Display name: 2–100 characters
- Account created with `EmailVerified = false`

### Technical Notes

- Domain event: `UserRegisteredEvent` → triggers verification email via Notifications module
- Creates related preference entities with defaults (lazy or event-driven)
- Guest cart merge happens at login (US-IDENT-004), not at registration

### Priority

Phase 1

---

## US-IDENT-002: Seller Application (Seller Onboarding)

**As a** verified buyer, **I want to** submit a seller application with business details, **so that** after admin approval I can create a storefront and sell products.

### Acceptance Criteria

- [ ] Given I am a verified buyer, When I submit a seller application with business name and documents, Then a `SellerApplication` is created with status `Pending`
- [ ] Given I have not verified my email, When I try to apply, Then I see an error requiring email verification first
- [ ] Given my application is approved by admin, Then I receive the `Seller` role and a Storefront draft is auto-created
- [ ] Given my application is rejected, Then I receive a notification with the rejection reason
- [ ] Given I already have an active or pending application, When I try to submit again, Then I see "Application already submitted"
- [ ] Given my application was previously rejected, Then I can submit a new application (new entity)
- [ ] Given I submit the application, Then admin is notified via `SellerApplicationSubmittedEvent`

### Business Rules

- Email must be verified before applying
- Application requires: BusinessName (mandatory), TaxId (optional), BusinessLicenseFileId (optional), IdentityDocFileId (optional), AdditionalNotes (optional)
- Each application is a separate aggregate with its own lifecycle
- Approval triggers `SellerApplicationApprovedEvent` → domain event handler assigns Seller role + creates Storefront draft
- Seller role is additive — user retains Buyer role
- Each seller can have exactly one Storefront

### State Machine

```
Pending → UnderReview → Approved
                     ↘ Rejected
Pending → Cancelled (applicant withdraws)
Rejected → (new application) → Pending
```

### Technical Notes

- Domain events: `SellerApplicationSubmittedEvent`, `SellerApplicationApprovedEvent`, `SellerApplicationRejectedEvent`
- `AssignSellerRoleHandler` (domain event handler): assigns Seller role + creates Storefront draft + notifies user
- File uploads pass through `IAntivirusScanner`
- Storefront creation is triggered automatically on approval

### Priority

Phase 1

---

## US-IDENT-003: Email Verification

**As a** registered user, **I want to** verify my email address via a 6-digit OTP code, **so that** I can access full platform features.

### Acceptance Criteria

- [ ] Given I registered, When I receive the OTP email and enter the code, Then my `EmailVerified` flag is set to `true`
- [ ] Given the verification code has expired (15 min), When I enter it, Then I see an error with option to resend
- [ ] Given I request a resend, When I click "Resend code", Then a new code is generated and emailed
- [ ] Given I am already verified, When I try to verify again, Then I see a message "Already verified"

### Business Rules

- Verification OTP: 6-character numeric code, valid for 15 minutes
- Max 3 resend attempts per hour (rate limiting)
- Email verification is required for: seller application, storefront activation
- Codes are stored hashed in `identity.email_verification_codes`

### Technical Notes

- Security notification — bypasses user notification preferences
- Template key: `security.email-verify`
- `POST /auth/verify-email { userId, code }`

### Priority

Phase 1

---

## US-IDENT-004: Login (JWT + Refresh Token)

**As a** registered user, **I want to** log in with email and password, **so that** I can access my account securely.

### Acceptance Criteria

- [ ] Given valid credentials, When I submit the login form, Then I receive a JWT access token and refresh token
- [ ] Given invalid credentials, When I submit the form, Then I see a generic error "Invalid email or password" (no enumeration)
- [ ] Given my account is banned, When I try to login, Then I see "Account suspended — contact support"
- [ ] Given I had items in a guest cart (session), When I login, Then those items merge into my persistent cart (quantity union, capped at stock)
- [ ] Given login from a new device/IP, Then a security notification email is sent

### Business Rules

- JWT access token TTL: configurable via `appsettings.json` (default 15 min)
- Refresh token TTL: configurable (default 7 days)
- Failed login attempts: lockout after 5 consecutive failures for 15 minutes
- Generic error message to prevent email enumeration attacks

### Technical Notes

- Security notification: `security.new-login` template — always sent, bypasses preferences
- Guest cart merge: handled via `CartMergeService` (Cart module contract)
- Refresh token stored hashed in DB
- JWT contains permission claims (`mn.perm.{module}`) resolved at login via `PermissionResolver`
- Token rotation on each refresh (old token invalidated)

### Priority

Phase 1

---

## US-IDENT-004a: Token Refresh

**As an** authenticated user, **I want** my session to silently refresh without re-entering credentials, **so that** I have a seamless experience within the 7-day refresh window.

### Acceptance Criteria

- [ ] Given a valid refresh token cookie, When I call refresh, Then I receive a new access token + new refresh token
- [ ] Given the old refresh token after rotation, When someone tries to use it, Then it's rejected (token rotation)
- [ ] Given the refresh token is expired or revoked, When I try to refresh, Then I receive 401 and must re-login

### Business Rules

- Refresh token rotation: each refresh invalidates the previous token
- Refresh re-resolves permissions (picks up any role/permission changes since last login)
- Expired/revoked tokens immediately rejected
- Redis blacklist for revoked JTIs (TTL = remaining access token expiry)

### Technical Notes

- `POST /auth/refresh` — cookie-based, no body needed
- `RefreshTokenCommandHandler` re-runs `PermissionResolver` to pick up role/permission changes

### Priority

Phase 1

---

## US-IDENT-005: Password Reset

**As a** user who forgot my password, **I want to** reset it via email, **so that** I can regain access to my account.

### Acceptance Criteria

- [ ] Given I request a password reset with a registered email, When submitted, Then I receive a reset link via email
- [ ] Given I request a reset with an unregistered email, When submitted, Then I see the same success message (no enumeration)
- [ ] Given a valid reset token, When I submit a new password, Then my password is updated and all refresh tokens are revoked
- [ ] Given an expired reset token (1h), When I submit, Then I see "Link expired — request a new one"

### Business Rules

- Reset token valid for 1 hour
- Max 3 reset requests per hour per email
- After successful reset: invalidate ALL existing refresh tokens (force re-login everywhere)
- Same generic response for registered/unregistered emails

### Technical Notes

- Security notification — bypasses preferences
- Template key: `security.password-reset`
- Variables: user name, reset URL, expiry time

### Priority

Phase 1

---

## US-IDENT-006: Profile Update

**As a** registered user, **I want to** update my profile information (phone, avatar, bio), **so that** my public profile is up to date.

### Acceptance Criteria

- [ ] Given I am on my profile page, When I update my display name, Then the change is saved
- [ ] Given I upload a valid avatar image, When saved, Then my `AvatarFileId` references the uploaded file
- [ ] Given I am a seller, When I edit my public bio, Then it is saved (max 500 chars)
- [ ] Given I am a buyer (not seller), Then the "Public Bio" field is not shown
- [ ] Given I enter an invalid phone number, When I save, Then I see a validation error

### Business Rules

- Email is read-only on profile page (change email is separate flow)
- Phone number: optional, validated as E.164 format
- Avatar: image upload pipeline (antivirus scan → store → reference)
- Public bio: only editable by Seller role, max 500 characters
- Display name: 2–100 characters

### Technical Notes

- File upload passes through `IAntivirusScanner`
- Avatar stored as `UploadedFile` entity reference (not raw URL)
- Uses shared form components: `_TextField`, `_PhoneField`, `_ImageUpload`

### Priority

Phase 1

---

## US-IDENT-007: Manage Addresses (CRUD)

**As a** buyer, **I want to** manage my saved shipping addresses, **so that** I can quickly select one during checkout.

### Acceptance Criteria

- [ ] Given I have fewer than 10 addresses, When I add a new address, Then it is saved with all required fields
- [ ] Given I already have 10 addresses, When I try to add another, Then I see "Maximum 10 addresses reached"
- [ ] Given I set an address as default, When saved, Then the previous default address loses its default flag
- [ ] Given I delete a non-default address, When confirmed, Then it is removed
- [ ] Given I try to delete the default address without setting another, Then I see an error
- [ ] Given an address is used in an active order and it's the only one, Then I cannot delete it

### Business Rules

- Max 10 addresses per user
- Exactly 1 default address at all times (unique partial index on `is_default = true`)
- Cannot delete default address unless another is set first
- Country code: ISO 3166-1 alpha-2 validated
- Address labels: Home | Office | Other

### Technical Notes

- Invariants 11, 12 from domain spec
- Unique partial index: `WHERE is_default = true AND user_id = ?`
- Uses `Address` value object validation rules

### Priority

Phase 1

---

## US-IDENT-008: User Preferences (Timezone, Date/Time Format, Language)

**As a** registered user, **I want to** set my timezone and display format preferences, **so that** dates and times appear in my local format.

### Acceptance Criteria

- [ ] Given I am on the Preferences settings tab, When I select a timezone from the dropdown, Then all dates in the app display in my timezone
- [ ] Given I select "24-hour" time format, When viewing timestamps, Then times show as HH:mm
- [ ] Given I select "Day/Month/Year" format, When viewing dates, Then dates show as DD/MM/YYYY
- [ ] Given I change my currency display preference, Then prices show with that currency symbol (display only, not conversion)

### Business Rules

- Timezone must be a valid IANA timezone ID
- Currency display is cosmetic only — does NOT convert amounts
- Language defaults to "en"; Phase 2 wires to localization
- All DB timestamps remain UTC; conversion at display time via `IUserTimeZoneProvider`
- Preferences auto-created with defaults on registration

### Technical Notes

- `IUserTimeZoneProvider` reads from `UserPreferences` (cached per request)
- Fallback chain: DB preference → cookie → UTC
- `DateTimeOffsetExtensions` methods use these preferences

### Priority

Phase 1

---

## US-IDENT-009: Notification Preferences

**As a** registered user, **I want to** control which notifications I receive and how often, **so that** I'm not overwhelmed by emails.

### Acceptance Criteria

- [ ] Given I am on Communications settings tab, When I toggle off "Order Shipped", Then I no longer receive that notification type
- [ ] Given I set frequency to "Daily Digest", Then non-urgent notifications batch into a single daily email at 9 AM my timezone
- [ ] Given I add an alternate email, When I verify it, Then I can choose to receive notifications there
- [ ] Given I try to set target to "Alternate" without verification, Then I see an error
- [ ] Given I am a seller, Then I see additional toggles: "Review Received", "Payment Processed"

### Business Rules

- Security notifications (password reset, new login) ALWAYS sent — cannot be toggled off
- Alternate email requires separate verification before use
- Notification target cannot include "Alternate" unless `AlternateEmailVerified = true`
- Seller-specific toggles only shown to Seller role
- Daily digest: 9:00 AM user's timezone

### Technical Notes

- `NotificationPreference` entity (1:1 with User)
- `INotificationPreferenceReadService` contract in Base.Common for cross-module reads
- Phase 2: full integration with notification dispatch pipeline

### Priority

Phase 1

---

## US-IDENT-010: Privacy Settings

**As a** registered user, **I want to** control my profile visibility and search discoverability, **so that** I can protect my privacy.

### Acceptance Criteria

- [ ] Given I set profile to "Private", When other users view my profile URL, Then they see limited information
- [ ] Given I am a seller with "Private" profile, Then my storefront is hidden from browse (but direct link still works)
- [ ] Given I disable "Allow Search", Then my profile/storefront doesn't appear in search results
- [ ] Given I have accepted the Terms, Then my consent date is displayed

### Business Rules

- Private profiles hide storefront from browse listings
- Reviews remain visible (linked to orders, not profile)
- `AllowSearch = false` excludes from `/search` results
- Terms consent date is immutable once set

### Technical Notes

- `UserPrivacy` entity (1:1 with User)
- Phase 2: GDPR data export, analytics consent toggles

### Priority

Phase 1

---

## US-IDENT-011: Change Password

**As a** registered user, **I want to** change my password from the Security settings tab, **so that** I can maintain account security.

### Acceptance Criteria

- [ ] Given I provide my current password correctly and a valid new password, When I submit, Then my password is updated
- [ ] Given I provide an incorrect current password, When I submit, Then I see "Current password is incorrect"
- [ ] Given password change succeeds, Then all other refresh tokens (other devices) are revoked
- [ ] Given the new password doesn't meet requirements, Then I see validation errors

### Business Rules

- Must provide current password for verification
- New password must meet minimum length and complexity rules
- After change: revoke all refresh tokens except current session
- Rate limit: max 3 password changes per hour

### Technical Notes

- Security notification: inform user of password change (always sent)
- Template key: `security.password-changed`

### Priority

Phase 1

---

## US-IDENT-012: Guest Cart Merge on Login

**As a** guest who added items to a session cart, **I want** my guest cart items to transfer to my account cart when I log in, **so that** I don't lose my selections.

### Acceptance Criteria

- [ ] Given I have 3 items in guest cart and 2 items in my account cart, When I login, Then all 5 items appear in my cart (if no duplicates)
- [ ] Given a guest cart item is the same variant as an existing cart item, When merged, Then quantities are summed (capped at stock/99)
- [ ] Given the merge would exceed 20 distinct items, Then the oldest guest cart items are dropped with a warning
- [ ] Given the merge completes, Then the guest session cart is cleared

### Business Rules

- Merge strategy: union by VariantId, sum quantities
- Quantity cap: min(sum, stock available, 99)
- Max distinct items: 20 per cart
- Guest cart is session-local (not persisted to DB)

### Technical Notes

- Cross-module: Identity triggers, Cart module handles the merge logic
- Contract: `ICartMergeService` or handled via `CartItemAddedEvent` sequence
- Happens at login time, not registration time

### Priority

Phase 1

