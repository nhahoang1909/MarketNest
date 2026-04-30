# Notifications Module

> **Version**: 1.0 | **Status**: Phase 1 Implemented | **Date**: 2026-04-30
> **Related**: `domain-and-business-rules.md` §8, `backend-patterns.md` §3, `notification-service-plan.md` (external spec)

---

## Table of Contents

1. [Overview](#1-overview)
2. [Domain Design](#2-domain-design)
3. [Template Engine](#3-template-engine)
4. [Dispatch Pipeline](#4-dispatch-pipeline)
5. [Email Channel](#5-email-channel)
6. [In-App Channel](#6-in-app-channel)
7. [Cross-Module Contract](#7-cross-module-contract)
8. [Background Jobs](#8-background-jobs)
9. [Seeding Default Templates](#9-seeding-default-templates)
10. [Module Structure](#10-module-structure)
11. [Configuration](#11-configuration)
12. [Phased Roadmap](#12-phased-roadmap)
13. [Usage Guide — Other Modules](#13-usage-guide--other-modules)

---

## 1. Overview

### Channel Strategy

| Channel | Phase | Realtime? | Persistence |
|---------|-------|-----------|-------------|
| **Email** | Phase 1 | No (async) | MailKit → MailHog (dev), SMTP (prod) |
| **In-App (Bell)** | Phase 1 | HTMX polling (30s) | `notifications.notifications` table |
| **SSE Push** | Phase 2 | Yes — push count update on new notification | SSE endpoint `/notifications/stream` |
| **SMS / Push** | Phase 4+ | — | Deferred |

### What the Module Owns

| Concern | Owner |
|---------|-------|
| `NotificationTemplate` (admin-managed templates) | `MarketNest.Notifications` |
| `Notification` (per-user in-app inbox) | `MarketNest.Notifications` |
| `NotificationLog` (dispatch audit trail) | `MarketNest.Notifications` — Phase 2 |
| `ITemplateRenderer` (Handlebars `{{Variable}}` engine) | `MarketNest.Notifications.Infrastructure` |
| `IEmailSender` (SMTP Phase 1 / Mailgun Phase 2) | `MarketNest.Notifications.Infrastructure` |
| `NotificationPreference` (user toggles, frequency) | **Identity module** — read via `INotificationPreferenceReadService` |

### Notification Type → Channel Matrix

| Type | Email | In-App | Always Send (security) |
|------|-------|--------|------------------------|
| Order placed | ✅ | ✅ | |
| Order confirmed | ✅ | ✅ | |
| Order shipped | ✅ | ✅ | |
| Order delivered (auto) | ✅ | ✅ | |
| Order cancelled | ✅ | ✅ | |
| Dispute opened | ✅ | ✅ | |
| Dispute seller responded | ✅ | ✅ | |
| Dispute resolved | ✅ | ✅ | |
| Payout processed | ✅ | ✅ | |
| Review received | ✅ (digest) | ✅ | |
| Inventory low | — | ✅ | Seller only |
| Password reset | ✅ | — | ✅ |
| New login unknown device | ✅ | — | ✅ |

---

## 2. Domain Design

### 2.1 Schema: `notifications.*`

```
notifications.notification_templates   ← Admin-managed templates (AdminDbContext also reads)
notifications.notifications            ← Per-user in-app inbox
notifications.notification_logs        ← Dispatch audit log (Phase 2)
```

### 2.2 NotificationTemplate (Aggregate Root)

```csharp
NotificationTemplate : AggregateRoot
├── Id: Guid
├── TemplateKey: string          // stable, immutable — "order.placed.buyer"
├── DisplayName: string          // Admin UI label
├── Channel: NotificationChannel // Email | InApp | Both
├── SubjectTemplate: string?     // email only — "Your order #{{OrderNumber}} placed!"
├── BodyTemplate: string         // HTML (email) or plain text (in-app), {{Variable}} style
├── AvailableVariables: string[] // documented vars — validated at save time
├── IsActive: bool               // false → use fallback, log warning
├── LastModifiedBy: Guid?        // admin userId
├── CreatedAt: DateTimeOffset
├── UpdatedAt: DateTimeOffset?
```

**Business Rules:**
- `TemplateKey` is **immutable after creation** — code constants reference it
- Admin can edit `SubjectTemplate`, `BodyTemplate`, `IsActive`, `DisplayName`
- Admin **cannot** change `Channel`, `TemplateKey` — requires code change
- Inactive template → fallback to hardcoded default, log `Warning` — **never silently drop**
- Seeded with defaults on startup (`RunInProduction = true`)
- Security templates (`security.*`) — `IsActive` is read-only in Admin UI (cannot deactivate)

**Domain Methods:**
```csharp
template.UpdateContent(subjectTemplate, bodyTemplate, modifiedBy)
template.Activate(modifiedBy)
template.Deactivate(modifiedBy)
```

### 2.3 Notification Entity (In-App Inbox)

```csharp
Notification : Entity<Guid>
├── Id: Guid
├── UserId: Guid             // recipient
├── TemplateKey: string      // "order.placed.buyer"
├── Title: string            // rendered, max 120 chars
├── Body: string             // rendered plain text, max 500 chars (HTML stripped)
├── ActionUrl: string?       // relative path: "/orders/abc-123" — no absolute URLs
├── IsRead: bool             // false on creation
├── ReadAt: DateTimeOffset?
├── CreatedAt: DateTimeOffset
├── ExpiresAt: DateTimeOffset // CreatedAt + 90 days
```

**Business Rules:**
- Notifications expire after 90 days (never user-deleted — soft-expired only)
- Mark-as-read → simple `UPDATE` — no domain event
- Max 200 unread per user — `CleanupExpiredNotificationsJob` enforces this
- Batch `mark-all-as-read` supported via `ExecuteUpdateAsync`
- `ActionUrl` always relative (prevents phishing via absolute redirect)

**Domain Method:**
```csharp
notification.MarkAsRead()
```

### 2.4 Enums

```csharp
public enum NotificationChannel { Email = 1, InApp = 2, Both = 3 }
public enum NotificationLogStatus { Sent = 1, Failed = 2, Skipped = 3 } // Phase 2
```

---

## 3. Template Engine

### 3.1 Handlebars-Style Variable Substitution

`HandlebarsTemplateRenderer` replaces `{{VariableName}}` tokens using `[GeneratedRegex]` (zero allocation):

```csharp
// Usage
var rendered = renderer.Render(template.BodyTemplate, variables);

// Template example
"Hi {{BuyerName}}, your order #{{OrderNumber}} has been placed."

// Variables dictionary
{ "BuyerName": "Alice", "OrderNumber": "ORD-0042", "OrderTotal": "₫450,000" }

// Result
"Hi Alice, your order #ORD-0042 has been placed."
```

**Rules:**
- Missing variable → token left unchanged (never crashes)
- No logic, no looping, no conditionals — simple string substitution only
- Upgrade to Scriban/Fluid if loops or conditionals are needed (Phase 3)

### 3.2 Template Key Constants

All stable template keys are defined in `MarketNest.Base.Common.NotificationTemplateKeys`:

```csharp
// Orders
NotificationTemplateKeys.OrderPlacedBuyer     = "order.placed.buyer"
NotificationTemplateKeys.OrderPlacedSeller    = "order.placed.seller"
NotificationTemplateKeys.OrderConfirmedBuyer  = "order.confirmed.buyer"
NotificationTemplateKeys.OrderShippedBuyer    = "order.shipped.buyer"
NotificationTemplateKeys.OrderDeliveredBuyer  = "order.delivered.buyer"
NotificationTemplateKeys.OrderCancelledBuyer  = "order.cancelled.buyer"
NotificationTemplateKeys.OrderCancelledSeller = "order.cancelled.seller"

// Disputes
NotificationTemplateKeys.DisputeOpenedSeller   = "dispute.opened.seller"
NotificationTemplateKeys.DisputeOpenedAdmin    = "dispute.opened.admin"
NotificationTemplateKeys.DisputeRespondedBuyer = "dispute.responded.buyer"
NotificationTemplateKeys.DisputeResolvedBuyer  = "dispute.resolved.buyer"
NotificationTemplateKeys.DisputeResolvedSeller = "dispute.resolved.seller"

// Payments
NotificationTemplateKeys.PayoutProcessedSeller = "payout.processed.seller"

// Catalog
NotificationTemplateKeys.ReviewReceivedSeller = "review.received.seller"
NotificationTemplateKeys.InventoryLowSeller   = "inventory.low.seller"

// Security (always sent — no toggle)
NotificationTemplateKeys.PasswordResetRequest  = "security.password-reset"
NotificationTemplateKeys.NewLoginUnknownDevice = "security.new-login"
```

> ⚠️ **Never add raw string template keys in handlers** — always use `NotificationTemplateKeys.*` constants.

### 3.3 Variable Records

Strongly-typed variable bags in `MarketNest.Base.Common` — one record per template family:

```csharp
// Orders
public record OrderPlacedVariables(
    string OrderNumber, string BuyerName, string SellerStoreName,
    string OrderTotal, string OrderUrl, string EstimatedDelivery);

public record OrderShippedVariables(
    string OrderNumber, string BuyerName,
    string TrackingNumber, string TrackingUrl, string OrderUrl);

// Disputes
public record DisputeOpenedVariables(
    string OrderNumber, string BuyerName,
    string DisputeReason, string DisputeUrl, string ResponseDeadline);

// Payments
public record PayoutProcessedVariables(
    string SellerName, string GrossAmount,
    string CommissionDeducted, string NetAmount, string PayoutUrl);

// Security
public record PasswordResetVariables(string UserName, string ResetUrl, string ExpiresIn);
public record NewLoginVariables(string UserName, string DeviceInfo, string IpAddress, string LoginTime);
```

**Convert to dictionary** for renderer via `ToVariables<T>()` extension:
```csharp
var vars = new OrderPlacedVariables(
    OrderNumber: order.Number,
    BuyerName: buyer.Name,
    SellerStoreName: storefront.Name,
    OrderTotal: order.BuyerTotal.ToString("C"),
    OrderUrl: $"/orders/{order.Id}",
    EstimatedDelivery: "5-7 business days"
).ToVariables();

await notifications.SendAsync(buyer.Id, NotificationTemplateKeys.OrderPlacedBuyer, vars, ct);
```

---

## 4. Dispatch Pipeline

```
INotificationService.SendAsync(userId, templateKey, variables)
│
├─ 1. Load NotificationTemplate by key (DB lookup)
│      └─ Not found or inactive? → log Warning, return (never throw)
│
├─ 2. [Phase 2] Load NotificationPreference via INotificationPreferenceReadService
│      └─ User opted out for this key? → log Debug "skipped-opt-out", return
│
├─ 3. Render: ITemplateRenderer.Render(template.BodyTemplate, variables)
│      + Render SubjectTemplate if email
│
├─ 4a. In-App (Channel = InApp or Both)
│       └─ new Notification(userId, templateKey, title, body, actionUrl)
│          notificationRepository.Add(notification)
│          [committed by UnitOfWork after handler returns]
│
├─ 4b. Email (Channel = Email or Both)
│       └─ Phase 1: log Debug "email-deferred" (awaits IUserEmailResolver from Identity)
│          Phase 2: IEmailLayoutRenderer.Wrap(body, baseUrl) → IEmailSender.SendAsync()
│
└─ 5. [Phase 2] NotificationLog INSERT (audit trace: Sent | Failed | Skipped)
```

**`SendSecurityEmailAsync`** (for password reset, new login):
- Skips step 2 (no preference check — always send)
- Sends directly to `toEmail` string (not resolved from userId)
- Wraps body in `EmailLayoutRenderer` branded layout
- Catches exceptions → logs Error, never throws (never fails the main request)

---

## 5. Email Channel

### 5.1 SmtpEmailSender (Phase 1)

Uses **MailKit** (v4.16.0+) to connect to MailHog in dev or real SMTP in production:

```csharp
public record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? PlainTextBody = null);
```

**Configuration** — bound from `appsettings.json` section `Smtp`:

```json
{
  "Smtp": {
    "Host": "localhost",
    "Port": 1025,
    "Username": "",
    "Password": "",
    "FromAddress": "noreply@marketnest.com",
    "FromName": "MarketNest",
    "UseSsl": false
  }
}
```

Phase 1 default points to MailHog (`localhost:1025`). Production: set `Host`, `Port`, credentials, `UseSsl: true`.

### 5.2 Email Layout

`EmailLayoutRenderer` wraps rendered body content in a hardcoded branded HTML layout:

```html
<!DOCTYPE html>
<html>
  <!-- MarketNest header + logo -->
  <div>{{ CONTENT }}</div>   ← admin-editable content inserted here
  <!-- MarketNest footer -->
</html>
```

Admin **cannot** modify the outer wrapper — only the inner `BodyTemplate` is editable. This prevents XSS/phishing via template editing.

### 5.3 Phase 2 — Mailgun / SendGrid

Phase 2 replaces `SmtpEmailSender` with `MailgunEmailSender` by swapping the `IEmailSender` DI registration in `DependencyInjection.cs` — no handler code changes.

---

## 6. In-App Channel

### 6.1 Architecture: HTMX Polling (Phase 1) → SSE Push (Phase 2)

```
Phase 1 (polling):
  Navbar bell icon
    └── hx-trigger="load, every 30s"
    └── hx-get="/notifications/unread-count"
    └── Server returns { count: N }
    └── Alpine updates badge number

Phase 2 (push):
  GET /notifications/stream (SSE endpoint)
    └── Server pushes { count: N } when notification created
    └── Alpine onNewNotification() updates badge immediately
```

### 6.2 Planned Razor Page Endpoints

| Route | Method | Description |
|-------|--------|-------------|
| `/notifications/unread-count` | GET | Returns `{ count: N }` for navbar polling |
| `/notifications/inbox` | GET | Returns `_NotificationDrawer` partial |
| `/notifications/inbox?page=N` | GET | Paged inbox (HTMX infinite scroll) |
| `/notifications/{id}/read` | POST | Marks single notification read, returns updated item |
| `/notifications/read-all` | POST | Marks all unread as read |

> These Razor Pages are **Phase 1 Frontend TODO** — backend CQRS handlers are already implemented.

### 6.3 CQRS Handlers (Implemented)

```csharp
MarkNotificationReadCommand(NotificationId, UserId)
MarkAllNotificationsReadCommand(UserId)
```

Both handlers follow standard CQRS patterns — return `Result<Unit, Error>`.
`MarkAllAsReadAsync` uses `ExecuteUpdateAsync` (bulk UPDATE, no entity loading).

---

## 7. Cross-Module Contract

### 7.1 INotificationService (Base.Common)

All modules communicate with the Notifications module exclusively via this interface:

```csharp
public interface INotificationService
{
    // Template-based dispatch — email and/or in-app per template config
    Task SendAsync(
        Guid recipientUserId,
        string templateKey,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken ct = default);

    // Send to multiple recipients (e.g., order.placed → buyer + seller)
    Task SendToMultipleAsync(
        IEnumerable<Guid> recipientUserIds,
        string templateKey,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken ct = default);

    // Security emails — bypasses preference check, always sent
    Task SendSecurityEmailAsync(
        string toEmail,
        string templateKey,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken ct = default);
}
```

### 7.2 Calling from Domain Event Handlers

```csharp
// MarketNest.Orders/Application/OrderPlacedNotificationHandler.cs
namespace MarketNest.Orders.Application;

public class OrderPlacedNotificationHandler(INotificationService notifications)
    : IDomainEventHandler<OrderPlacedEvent>
{
    public async Task Handle(OrderPlacedEvent evt, CancellationToken ct)
    {
        var vars = new OrderPlacedVariables(
            OrderNumber: evt.OrderNumber,
            BuyerName: evt.BuyerName,
            SellerStoreName: evt.SellerStoreName,
            OrderTotal: evt.Total.ToString("C"),
            OrderUrl: $"/orders/{evt.OrderId}",
            EstimatedDelivery: "5-7 business days"
        ).ToVariables();

        // Send to buyer
        await notifications.SendAsync(evt.BuyerId, NotificationTemplateKeys.OrderPlacedBuyer, vars, ct);

        // Send to seller
        await notifications.SendAsync(evt.SellerId, NotificationTemplateKeys.OrderPlacedSeller, vars, ct);
    }
}
```

> Domain event handlers are **post-commit** (dispatched AFTER TX commit per ADR-027). Notification failures are caught and logged — they never fail the main request.

### 7.3 INotificationPreferenceReadService (Phase 2)

The dispatch pipeline will check user preferences before sending:

```csharp
// Base.Common/Contracts — implemented by Identity module
public interface INotificationPreferenceReadService
{
    Task<NotificationPreferenceSnapshot?> GetByUserIdAsync(Guid userId, CancellationToken ct);
}

public record NotificationPreferenceSnapshot(
    bool NotifyOrderPlaced,
    bool NotifyOrderConfirmed,
    bool NotifyOrderShipped,
    bool NotifyOrderDelivered,
    bool NotifyDisputeOpened,
    bool NotifyDisputeResolved,
    bool NotifyReviewReceived,
    bool NotifyPaymentProcessed,
    NotificationFrequency Frequency,
    string? AlternateEmail,
    bool AlternateEmailVerified);
```

**Phase 1**: Preference check is **not yet applied** — all notifications sent regardless of user preference. Integration deferred until Identity module implements `NotificationPreference` entity.

---

## 8. Background Jobs

### 8.1 CleanupExpiredNotificationsJob (Phase 1 ✅ Implemented)

```
Job Key:  "notifications.cleanup-expired"
Schedule: Daily 03:00 UTC (cron: "0 3 * * *")
Module:   Notifications
```

**Logic:**
1. `DELETE notifications WHERE expires_at < NOW() AND is_read = true`
2. Background job manages its own UoW transaction (runs outside HTTP pipeline)

### 8.2 ProcessHourlyDigestJob (Phase 2)

```
Job Key:  "notifications.hourly-digest"
Schedule: Every hour (cron: "0 * * * *")
```

**Logic:** Find all queued digest entries for `Frequency = OneHourDigest` → group by user → compose digest email → send via `IEmailSender` → mark sent.

### 8.3 ProcessDailyDigestJob (Phase 2)

```
Job Key:  "notifications.daily-digest"
Schedule: Every 30 min (check against user timezone)
```

**Logic:** Find users with `Frequency = DailyDigest` where current UTC == 09:00 AM in user's timezone → compose digest → send.

---

## 9. Seeding Default Templates

`NotificationTemplateSeeder` seeds 17 default templates on startup:

| Template Key | Channel | Subject |
|---|---|---|
| `order.placed.buyer` | Both | Your order #{{OrderNumber}} has been placed! |
| `order.placed.seller` | Both | New order #{{OrderNumber}} received! |
| `order.confirmed.buyer` | Both | Your order #{{OrderNumber}} has been confirmed |
| `order.shipped.buyer` | Both | Your order #{{OrderNumber}} has been shipped! |
| `order.delivered.buyer` | Both | Your order #{{OrderNumber}} has been delivered |
| `order.cancelled.buyer` | Both | Your order #{{OrderNumber}} has been cancelled |
| `order.cancelled.seller` | Both | Order #{{OrderNumber}} has been cancelled |
| `dispute.opened.seller` | Both | A dispute has been opened for order #{{OrderNumber}} |
| `dispute.opened.admin` | Both | New dispute for order #{{OrderNumber}} |
| `dispute.responded.buyer` | Both | Seller responded to your dispute for order #{{OrderNumber}} |
| `dispute.resolved.buyer` | Both | Your dispute for order #{{OrderNumber}} has been resolved |
| `dispute.resolved.seller` | Both | Dispute for order #{{OrderNumber}} has been resolved |
| `payout.processed.seller` | Both | Your payout of {{NetAmount}} has been processed |
| `review.received.seller` | Both | New {{Rating}}-star review on {{ProductName}} |
| `inventory.low.seller` | InApp | *(in-app only)* |
| `security.password-reset` | Email | Reset your MarketNest password |
| `security.new-login` | Email | New login to your MarketNest account |

**Seeder behavior:**
- `Order = 350` — runs after RoleSeeder (100), AdminUserSeeder (200), CategorySeeder (300)
- `RunInProduction = true` — safe reference data
- **Only inserts missing keys** — never overwrites admin-customized templates
- `Version = "2026.04.30"` — bump to re-run seeder after adding new templates

---

## 10. Module Structure

```
src/MarketNest.Notifications/
│
├── Domain/
│   ├── Entities/
│   │   ├── NotificationTemplate.cs     ← Aggregate Root
│   │   └── Notification.cs             ← Entity (in-app inbox item)
│   └── Enums/
│       ├── NotificationChannel.cs      ← Email | InApp | Both
│       └── NotificationLogStatus.cs    ← Sent | Failed | Skipped (Phase 2)
│
├── Application/
│   ├── Contracts/
│   │   ├── ITemplateRenderer.cs        ← Template rendering abstraction
│   │   ├── IEmailSender.cs + EmailMessage ← Email sending abstraction
│   │   └── IEmailLayoutRenderer.cs     ← HTML layout wrapping abstraction
│   ├── Repositories/
│   │   ├── INotificationTemplateRepository.cs
│   │   └── INotificationRepository.cs
│   ├── Queries/
│   │   └── IGetNotificationInboxQuery.cs + IGetUnreadCountQuery.cs
│   ├── Dtos/
│   │   └── NotificationItemDto.cs
│   ├── Commands/
│   │   └── MarkNotificationReadCommand.cs  ← MarkNotificationReadCommand + MarkAllNotificationsReadCommand
│   ├── CommandHandlers/
│   │   └── MarkNotificationReadHandler.cs
│   └── Services/
│       └── NotificationService.cs      ← INotificationService implementation
│
└── Infrastructure/
    ├── DependencyInjection.cs          ← AddNotificationsModule()
    ├── Persistence/
    │   ├── NotificationsDbContext.cs   ← IModuleDbContext, schema = "notifications"
    │   ├── NotificationsReadDbContext.cs
    │   ├── BaseRepository.cs           ← 2-line thin wrapper
    │   ├── BaseQuery.cs                ← 2-line thin wrapper
    │   └── Configurations/
    │       ├── NotificationTemplateConfiguration.cs
    │       └── NotificationConfiguration.cs
    ├── Repositories/
    │   ├── NotificationTemplateRepository.cs
    │   └── NotificationRepository.cs
    ├── Queries/
    │   └── NotificationInboxQuery.cs   ← GetNotificationInboxQuery + UnreadCountQuery
    ├── Services/
    │   ├── HandlebarsTemplateRenderer.cs
    │   ├── EmailLayoutRenderer.cs
    │   ├── SmtpEmailSender.cs          ← MailKit, Phase 1
    │   └── SmtpOptions.cs
    ├── Jobs/
    │   └── CleanupExpiredNotificationsJob.cs
    └── Seeders/
        └── NotificationTemplateSeeder.cs

src/Base/MarketNest.Base.Common/
└── Contracts/Contracts/
    ├── INotificationService.cs
    └── Notifications/
        ├── NotificationTemplateKeys.cs
        ├── NotificationVariables.cs
        └── NotificationVariableExtensions.cs
```

---

## 11. Configuration

### 11.1 DI Registration (`Program.cs`)

```csharp
builder.Services.AddNotificationsModule(builder.Configuration);

// Also registered in:
builder.Services.AddModuleInfrastructureServices(
    typeof(MarketNest.Notifications.AssemblyReference).Assembly, ...);

builder.Services.AddDatabaseInitializer(
    typeof(MarketNest.Notifications.AssemblyReference).Assembly, ...);
```

### 11.2 appsettings.json

```json
{
  "Smtp": {
    "Host": "localhost",
    "Port": 1025,
    "Username": "",
    "Password": "",
    "FromAddress": "noreply@marketnest.com",
    "FromName": "MarketNest",
    "UseSsl": false
  }
}
```

For production, override via `.env` or environment variable:
```
Smtp__Host=smtp.mailgun.org
Smtp__Port=587
Smtp__Username=postmaster@mg.marketnest.com
Smtp__Password=<secret>
Smtp__UseSsl=true
```

### 11.3 Connection Strings

Follows ADR-031 two-connection-string pattern:

```csharp
// In DependencyInjection.cs
string writeConnection = configuration.GetConnectionString("DefaultConnection") ?? throw ...;
string readConnection  = configuration.GetConnectionString("ReadConnection")
                            is { Length: > 0 } rc ? rc : writeConnection;
```

---

## 12. Phased Roadmap

### Phase 1 ✅ (Implemented 2026-04-30)

- [x] `NotificationTemplate` aggregate root
- [x] `Notification` entity (in-app inbox)
- [x] `NotificationsDbContext` + `NotificationsReadDbContext`
- [x] `HandlebarsTemplateRenderer`
- [x] `SmtpEmailSender` (MailKit → MailHog)
- [x] `EmailLayoutRenderer` (hardcoded branded wrapper)
- [x] `INotificationService` dispatch pipeline (in-app only; email deferred pending `IUserEmailResolver`)
- [x] `SendSecurityEmailAsync` (direct email, no preference check)
- [x] `MarkNotificationReadCommand` + `MarkAllNotificationsReadCommand`
- [x] `GetNotificationInboxQuery` + `GetUnreadCountQuery`
- [x] `NotificationTemplateSeeder` (17 default templates)
- [x] `CleanupExpiredNotificationsJob` (daily, 03:00 UTC)
- [x] `NotificationTemplateKeys` constants
- [x] Variable records + `ToVariables<T>()` extension

**Phase 1 Frontend TODO:**
- [ ] Navbar bell icon (`_Layout.cshtml` + HTMX polling)
- [ ] `notificationBell` Alpine component (`wwwroot/js/components/notificationBell.js`)
- [ ] `_NotificationDrawer.cshtml` (side deck partial)
- [ ] `_NotificationItem.cshtml` (single item partial)
- [ ] Razor Pages: `NotificationsController` or inline Razor endpoints for unread-count, inbox, read, read-all
- [ ] Register routes in `AppRoutes` + `WhitelistedPrefixes`

### Phase 2

- [ ] `NotificationLog` entity — audit trail (userId, templateKey, channel, status, skipReason, renderedSubject)
- [ ] `INotificationPreferenceReadService` integration — check user opt-out before dispatch
- [ ] Complete email dispatch (resolve user email from Identity via `IUserEmailResolver` contract)
- [ ] SSE endpoint `/notifications/stream` — push count update replacing 30s polling
- [ ] `ProcessHourlyDigestJob` + `ProcessDailyDigestJob`
- [ ] Mailgun/SendGrid `IEmailSender` implementation (replace SMTP for production)
- [ ] Unsubscribe flow: `GET /notifications/unsubscribe?token=...`
- [ ] Admin UI: `/admin/notifications/templates` list + edit page
- [ ] Admin UI: `/admin/notifications/log` query page

### Phase 3 (Microservice Extraction)

- [ ] Extract `MarketNest.Notifications` → standalone service
- [ ] Consume domain events via RabbitMQ (MassTransit) — replace MediatR in-process
- [ ] Outbox pattern for emails: INSERT → background worker polls and sends
- [ ] `IntegrationEventConsumerAdapter<T>` — reuse existing handlers as MassTransit consumers
- [ ] Rate limiting: max N emails/hour per user

---

## 13. Usage Guide — Other Modules

### Adding a New Notification Type

**Step 1** — Add constant to `NotificationTemplateKeys`:
```csharp
// src/Base/MarketNest.Base.Common/Contracts/Contracts/Notifications/NotificationTemplateKeys.cs
public const string StorefrontActivatedSeller = "storefront.activated.seller";
```

**Step 2** — Add variable record to `NotificationVariables.cs`:
```csharp
public record StorefrontActivatedVariables(
    string SellerName,
    string StorefrontName,
    string StorefrontUrl);
```

**Step 3** — Add seed data to `NotificationTemplateSeeder.cs`:
```csharp
new NotificationTemplate(
    templateKey: NotificationTemplateKeys.StorefrontActivatedSeller,
    displayName: "Storefront Activated — Seller",
    channel: NotificationChannel.Both,
    subjectTemplate: "Your storefront '{{StorefrontName}}' is now live!",
    bodyTemplate: """
        <p>Hi {{SellerName}}, your storefront <strong>{{StorefrontName}}</strong> has been activated.</p>
        <p><a href="{{StorefrontUrl}}">View your storefront →</a></p>
        """,
    availableVariables: ["SellerName", "StorefrontName", "StorefrontUrl"])
```

**Step 4** — Bump seeder version: `Version => "2026.05.01"`

**Step 5** — Create domain event handler in the module that owns the event:
```csharp
// MarketNest.Catalog/Application/StorefrontActivatedNotificationHandler.cs
namespace MarketNest.Catalog.Application;

public class StorefrontActivatedNotificationHandler(INotificationService notifications)
    : IDomainEventHandler<StorefrontActivatedEvent>
{
    public async Task Handle(StorefrontActivatedEvent evt, CancellationToken ct)
    {
        var vars = new StorefrontActivatedVariables(
            SellerName: evt.SellerName,
            StorefrontName: evt.StorefrontName,
            StorefrontUrl: $"/s/{evt.Slug}"
        ).ToVariables();

        await notifications.SendAsync(evt.SellerId, NotificationTemplateKeys.StorefrontActivatedSeller, vars, ct);
    }
}
```

### Security Notifications

For security emails (password reset, new login), use `SendSecurityEmailAsync` — preference check is bypassed:

```csharp
// In Identity module — password reset flow
var vars = new PasswordResetVariables(
    UserName: user.Name,
    ResetUrl: $"/auth/reset-password?token={token}",
    ExpiresIn: "30 minutes"
).ToVariables();

await notifications.SendSecurityEmailAsync(
    toEmail: user.Email,
    templateKey: NotificationTemplateKeys.PasswordResetRequest,
    variables: vars,
    ct: ct);
```

### Logging Event IDs (Notifications Module Block)

Notifications module owns EventId block `90000–99999` (ADR-033):

| EventId | Event |
|---------|-------|
| 90000+ | Infrastructure events (DB, email sender) |
| 92000+ | Application events (dispatch, skip) |
| 96000+ | Background job events |

See `LogEventId.cs` for exact assignments as the module grows.

