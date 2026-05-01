# Notifications Module — Functional Specification

> Module: `MarketNest.Notifications` | Schema: `notifications` | Version: 1.0 | Date: 2026-05-01

## Module Overview

The Notifications module provides template-based dispatch of email and in-app notifications triggered by domain events across the platform. It supports configurable delivery channels, batched digests, and security notifications that bypass user preferences.

## Actors

| Actor | Relevant Actions |
|-------|-----------------|
| Buyer | Receive notifications, view in-app inbox |
| Seller | Receive notifications, view in-app inbox |
| Admin | Receive escalation notifications |
| System | Dispatch on domain events, batch digests |

---

## US-NOTIF-001: Send Notification on Domain Event

**As the** platform, **I want** notifications to be automatically dispatched when domain events occur, **so that** users are informed of important actions.

### Acceptance Criteria

- [ ] Given `OrderPlacedEvent` is raised, When processed, Then buyer receives "Order placed" notification and seller receives "New order" notification
- [ ] Given `OrderShippedEvent` is raised, When processed, Then buyer receives tracking information notification
- [ ] Given any notification is sent, Then it uses the registered template with variable substitution
- [ ] Given notification dispatch fails, Then the failure is logged but the main request is NOT affected
- [ ] Given template variables are provided, Then placeholders like `{{BuyerName}}`, `{{OrderId}}` are replaced

### Business Rules

- Notifications are post-commit (dispatched AFTER transaction commit per ADR-027)
- Notification failures are caught and logged — never fail the main request
- Template-based: each notification type has a registered template key
- Variables: type-safe variable objects converted to dictionary for template engine
- Channels: Email + In-App (both by default)

### Technical Notes

- `INotificationService` contract in Base.Common
- Template keys: `order.placed.buyer`, `order.placed.seller`, `order.shipped.buyer`, etc.
- See §8.1 Notification → Template Key Mapping in domain spec
- Handlers are post-commit domain event handlers (ADR-027)
- 17 default templates seeded on startup

### Priority

Phase 1

---

## US-NOTIF-002: Security Notifications Bypass Preferences

**As the** platform, **I want** security notifications to always be sent regardless of user preferences, **so that** users are protected from unauthorized access.

### Acceptance Criteria

- [ ] Given a password reset is requested, Then the email is sent regardless of notification toggles
- [ ] Given a login from a new device/IP, Then the security alert is sent regardless of toggles
- [ ] Given the user has disabled email notifications, Then security notifications are still delivered
- [ ] Given the security email uses the primary email (never alternate), Then it arrives at the registered address

### Business Rules

- Security notifications: ALWAYS sent, cannot be toggled off
- Templates: `security.password-reset`, `security.new-login`
- Always sent to primary email (not alternate)
- Cannot be batched into digests
- Bypasses `INotificationPreferenceReadService` entirely

### Technical Notes

- `SendSecurityEmailAsync()` method on INotificationService
- Does not check NotificationPreference toggles
- Email channel only (no in-app for security alerts)

### Priority

Phase 1

---

## US-NOTIF-003: Respect User Notification Toggles

**As a** user, **I want** my notification preferences to be respected, **so that** I only receive notifications I've opted into.

### Acceptance Criteria

- [ ] Given I disabled "Order Shipped" notifications, When an order ships, Then I don't receive that notification
- [ ] Given I have all toggles enabled (default), Then I receive all notification types
- [ ] Given a new notification type is added, Then it defaults to enabled (opt-out model)
- [ ] Given my preference is for alternate email, When a notification sends, Then it goes to my verified alternate email

### Business Rules

- Opt-out model: all enabled by default, user disables what they don't want
- Toggle check: performed before dispatch (except security notifications)
- Alternate email target requires `AlternateEmailVerified = true`
- Seller-specific toggles only affect Seller role users

### Technical Notes

- `INotificationPreferenceReadService` contract (Base.Common)
- Phase 2: full integration with dispatch pipeline
- Phase 1: basic toggle checking implemented

### Priority

Phase 1 (basic) / Phase 2 (full integration)

---

## US-NOTIF-004: Daily Digest Batching

**As a** user who selected "Daily Digest" frequency, **I want** my notifications to be batched and sent once per day at 9 AM my timezone, **so that** I'm not overwhelmed by individual emails.

### Acceptance Criteria

- [ ] Given my frequency is "Daily Digest", When notifications occur throughout the day, Then they're queued
- [ ] Given it's 9:00 AM in my timezone, When the digest job runs, Then I receive one email with all pending notifications
- [ ] Given I have no pending notifications at digest time, Then no email is sent
- [ ] Given my frequency is "Real Time", Then notifications are sent immediately (no batching)

### Business Rules

- Digest time: 9:00 AM user's timezone (from UserPreferences.Timezone)
- Frequency options: RealTime | OneHourDigest | DailyDigest
- Security notifications: never batched (always immediate)
- Empty digest: no email sent

### Technical Notes

- Background job: `ProcessDailyNotificationDigest`
- Job key: `notifications.digest.daily`
- Schedule: Daily 09:00 (processes per user timezone)
- Requires UserPreferences.Timezone for timezone-aware scheduling
- Cross-module: reads from Identity via `IUserPreferencesReadService`

### Priority

Phase 1

---

## US-NOTIF-005: In-App Notification Inbox

**As a** user, **I want to** view my notifications in an in-app inbox, **so that** I can see recent activity without checking email.

### Acceptance Criteria

- [ ] Given I am logged in, When I click the notification bell, Then I see my recent in-app notifications
- [ ] Given I have unread notifications, Then the bell shows an unread count badge
- [ ] Given I click on a notification, Then it's marked as read and I'm navigated to the relevant page
- [ ] Given I click "Mark all as read", Then all unread notifications are marked as read
- [ ] Given notifications are listed, Then they show: title, summary, timestamp (relative), and read/unread status

### Business Rules

- In-app notifications: persisted in DB (not just email)
- Unread count: real-time via HTMX polling or lazy-load
- Notification links to relevant entity (order details, dispute, etc.)
- Retention: notifications kept for 90 days, then auto-archived

### Technical Notes

- Entity: InAppNotification (in notifications schema)
- query: paginated, sorted by date descending
- HTMX: lazy-load bell badge count
- UI: dropdown panel or dedicated page

### Priority

Phase 1

---

## US-NOTIF-006: Alternate Email Delivery

**As a** user, **I want to** receive notifications at a verified alternate email, **so that** I can separate personal and marketplace communications.

### Acceptance Criteria

- [ ] Given I add an alternate email in settings, When added, Then a verification email is sent to that address
- [ ] Given I verify the alternate email, When verified, Then I can select it as notification target
- [ ] Given I select target "Alternate", When notifications send, Then they go to the alternate email
- [ ] Given I select target "Both", Then notifications are sent to both primary and alternate emails
- [ ] Given my alternate email is unverified, When I try to set target to "Alternate", Then I see error

### Business Rules

- Alternate email: separate from primary (login) email
- Must be verified before use as notification target
- Target options: Primary | Alternate | Both
- Verification: separate flow from primary email verification
- Can be removed/changed at any time (re-verification required)

### Technical Notes

- Fields on `NotificationPreference`: AlternateEmail, AlternateEmailVerified, NotificationTarget
- Verification: similar to email verification flow (token-based)
- Validation: `MustBeValidEmail()` from ValidatorExtensions

### Priority

Phase 1

