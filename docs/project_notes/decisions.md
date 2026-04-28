# Architectural Decisions

Architectural Decision Records (ADRs) for MarketNest. Number sequentially. Keep all entries — they provide historical context.

**Review cadence**: Review quarterly. Mark outdated decisions as `**Status**: Superseded by ADR-XXX` — **never delete** old ADRs. Future developers need the "why" behind legacy code.

**Keep entries concise**: Each ADR should be scannable in 30 seconds. Link to external docs for lengthy analysis.

**When this file exceeds ~20 entries**: Add a Table of Contents at the top.

## Format

### ADR-XXX: Title (YYYY-MM-DD)

**Context:**
- Why the decision was needed

**Decision:**
- What was chosen

**Alternatives Considered:**
- Option → Why rejected

**Consequences:**
- ✅ Benefits
- ❌ Trade-offs

---

## Decisions

### ADR-001: Modular Monolith → Microservices → Kubernetes Phased Architecture (2026-04-25)

**Context:**
- Solo learning project aimed at mastering distributed systems progressively
- Need to deliver a working product early while building toward cloud-native patterns
- Starting with microservices would be premature without a working domain first

**Decision:**
- Phase 1 (months 1–3): Single .NET 10 deployable with schema-per-module PostgreSQL
- Phase 3 (months 6–7): Extract Notification Service, add RabbitMQ/MassTransit + YARP gateway
- Phase 4 (months 8–9): Kubernetes (kind locally, AKS/EKS cloud) with Helm + ArgoCD

**Alternatives Considered:**
- Start with microservices → Rejected: too complex before domain is understood
- Stay as monolith → Rejected: doesn't meet learning goal of distributed systems

**Consequences:**
- ✅ Working product by month 3
- ✅ Progressive complexity — each phase builds on established patterns
- ✅ Clear exit criteria per phase
- ❌ Some refactoring required at phase boundaries (in-process → RabbitMQ events)

---

### ADR-002: Razor Pages + HTMX + Alpine.js (No SPA Framework) (2026-04-25)

**Context:**
- Need interactive UI without the complexity of a full SPA build pipeline
- Server-side rendering preferred for SEO and simplicity
- Want reactive components without shipping a large JS framework

**Decision:**
- Razor Pages for server-rendered HTML
- HTMX 2 for partial page updates (replaces full-page reloads)
- Alpine.js 3 for client-side reactive components
- Tailwind CSS 4 for styling

**Alternatives Considered:**
- React/Next.js → Rejected: adds SPA complexity to a backend-focused learning project
- Blazor Server → Rejected: SignalR overhead for every interaction; less standard
- Vue.js → Rejected: same SPA complexity argument

**Consequences:**
- ✅ Simple mental model: HTML over the wire
- ✅ No separate frontend build/deploy pipeline in Phase 1
- ✅ Progressive enhancement by default
- ❌ Less component reuse compared to React ecosystem

---

### ADR-003: Result<T, Error> — No Exceptions for Business Failures (2026-04-25)

**Context:**
- Need explicit error handling that forces callers to handle failures
- Exceptions are expensive and their propagation is implicit
- Railway-oriented programming makes error flow visible in the type system

**Decision:**
- All application-layer methods return `Result<T, Error>`
- Exceptions reserved for truly exceptional infrastructure failures (DB unreachable, etc.)
- Error codes use `DOMAIN.ENTITY_ERROR` format (e.g., `ORDER.NOT_FOUND`)

**Alternatives Considered:**
- Throw exceptions everywhere → Rejected: implicit propagation, hard to reason about
- Nullable returns → Rejected: doesn't distinguish between "not found" and "failed"

**Consequences:**
- ✅ All failure paths are explicit and compiler-enforced
- ✅ Clean error codes for API responses
- ❌ Slightly more verbose than try/catch
- ❌ Callers must unwrap results (but this is the point)

---

### ADR-004: EF Core with Schema-Per-Module Boundary Enforcement (2026-04-25)

**Context:**
- Need physical enforcement of module boundaries in Phase 1 monolith
- Modules must not query each other's tables even though they share one DB
- Migrating to separate DBs in Phase 3 should be low-friction

**Decision:**
- Each module owns a separate PostgreSQL schema (e.g., `identity.*`, `orders.*`)
- Each module has its own `DbContext` — no cross-schema joins in EF
- Cross-module data needs go through service interfaces or domain events

**Alternatives Considered:**
- Single shared DbContext → Rejected: too easy to accidentally join across modules
- Separate databases from day one → Rejected: distributed transactions complexity in Phase 1

**Consequences:**
- ✅ Module isolation enforced by DB schema boundaries
- ✅ Easy to split to separate DBs in Phase 3
- ❌ Slight duplication of shared lookup data (e.g., user display name in orders)

---

### ADR-005: No Magic Strings / Magic Numbers — All Literals Must Be Named Constants or Enums (2026-04-25)

**Context:**
- Codebase will grow across 10+ modules; scattered string/numeric literals become hard to refactor and easy to mistype
- Repeated values like Redis key prefixes, route paths, commission rates, and retry counts need a single source of truth

**Decision:**
- Every string literal used more than once and every unexplained numeric literal must be extracted to a `const`, `static readonly`, enum, or strongly-typed configuration option
- Exceptions: `0`, `1`, `-1`, `string.Empty`, and obvious boolean comparisons
- Rule documented in `docs/code-rules.md` §2.5 and enforced via PR checklist

**Alternatives Considered:**
- Rely on code review alone → Rejected: too easy to miss, inconsistent enforcement
- Roslyn analyzer (e.g., CA1802/CA1805) → Considered for future; manual rule sufficient for now

**Consequences:**
- ✅ Single source of truth for all repeated values — rename once, change everywhere
- ✅ Reduces typo-related bugs (e.g., misspelled Redis key prefix)
- ✅ Improves readability — named constants communicate intent
- ❌ Slightly more boilerplate for one-off constants

---

### ADR-006: System Tables in `public` Schema, Module Tables in Named Schemas (2026-04-25)

**Context:**
- ADR-004 established schema-per-module for isolation
- System-level tracking tables (`__auto_migration_history`, `__seed_history`) previously lived in a custom `_system` schema
- PostgreSQL always has a `public` schema — using it for system tables is more conventional and avoids creating a non-standard schema

**Decision:**
- System-level tables (`__auto_migration_history`, `__seed_history`) live in the `public` schema
- Each module's domain tables live in their own named schema (e.g., `identity`, `catalog`, `orders`)
- `DatabaseInitializer` creates module schemas (`CREATE SCHEMA IF NOT EXISTS`) before running migrations

**Alternatives Considered:**
- Keep `_system` schema → Rejected: `public` is more conventional for shared/system tables in PostgreSQL
- Put everything in `public` → Rejected: loses module isolation benefits from ADR-004

**Consequences:**
- ✅ Follows PostgreSQL conventions — `public` is the natural home for shared system tables
- ✅ No need to create a custom `_system` schema on fresh databases
- ✅ Module isolation preserved — each module still owns its own schema
- ❌ None significant

---

### ADR-007: DDD Property Accessor Convention — Entities vs Value Objects (2026-04-25)

**Context:**
- DDD requires entities to protect their internal state — mutations must go through explicit domain methods
- Value objects are immutable by definition — once created, their state never changes
- Need a clear, enforceable convention for property accessors across all modules

**Decision:**
- **Entities** (including Aggregate Roots): all properties use `{ get; private set; }`. State changes only through domain methods. Exception: `Entity<TKey>.Id` uses `{ get; protected set; }` so derived classes can initialize it.
- **Value Objects (class-based, extending `ValueObject`)**: all properties use `{ get; }` (readonly, set only via constructor).
- **Value Objects (record-based)**: use positional records (which yield `{ get; init; }`) or explicit `{ get; }` / `{ get; init; }` properties.
- **DTOs / Commands / Queries**: use `record` with `{ get; init; }` — immutable after creation but settable during initialization.
- **Infrastructure interfaces** (`ISoftDeletable`, `IAuditable`): `{ get; set; }` is allowed because EF Core interceptors need write access.

**Alternatives Considered:**
- Allow `{ get; init; }` on entities → Rejected: `init` allows setting during object initializer, bypassing domain method guards
- Use `{ get; }` on entities → Rejected: entities need internal state mutation through domain methods; `private set` is necessary

**Consequences:**
- ✅ Entity invariants enforced — no external code can bypass domain methods
- ✅ Value object immutability guaranteed at the compiler level
- ✅ Clear, grep-able convention — easy to verify in code review and architecture tests
- ❌ Requires EF Core `HasField()` or backing field configuration for some entity properties

---

### ADR-008: Integration Event Infrastructure — Transport-Agnostic Event Bus (2026-04-25)

**Context:**
- Bounded contexts (modules) need to communicate asynchronously via events
- Phase 1 runs in-process; Phase 3 moves to RabbitMQ/MassTransit
- Need to avoid rewriting all event handlers when migrating to message broker

**Decision:**
- Separate `IIntegrationEvent` from `IDomainEvent` — domain events stay intra-aggregate, integration events cross module boundaries
- `IEventBus` abstraction with `PublishAsync<TEvent>()` — modules depend only on this interface
- Phase 1: `InProcessEventBus` wraps MediatR `IPublisher` (in-process dispatch)
- Phase 3: Swap to `MassTransitEventBus` wrapping `IPublishEndpoint` — one DI registration change
- `IIntegrationEventHandler<TEvent>` extends MediatR `INotificationHandler<TEvent>` so handlers are auto-discovered
- Phase 3 bridge: `IntegrationEventConsumerAdapter<TEvent>` wraps existing handlers as MassTransit `IConsumer<T>`
- Integration event contracts (records) live in `MarketNest.Core/Common/Events/IntegrationEvents/` — shared across all modules
- All integration events inherit from `IntegrationEvent` base record (provides `EventId` + `OccurredAtUtc`)

**Alternatives Considered:**
- Use `IDomainEvent` for everything → Rejected: conflates intra-aggregate and cross-module semantics; harder to add outbox selectively
- Depend on MassTransit directly in Phase 1 → Rejected: unnecessary RabbitMQ dependency before Phase 3
- Custom event dispatcher without MediatR → Rejected: duplicates existing infrastructure; MediatR already handles DI resolution

**Consequences:**
- ✅ Zero handler code changes when migrating Phase 1 → Phase 3
- ✅ Clear semantic distinction: domain events (internal) vs integration events (cross-module)
- ✅ `EventId` on every integration event enables idempotency/dedup in Phase 3
- ❌ Slight indirection — in-process dispatch goes through `IEventBus` → MediatR instead of direct `IPublisher`

---

### ADR-009: Pre-commit Secret Detection with Gitleaks (2026-04-25)

**Context:**
- Project uses `.env` files for secrets (DB passwords, API keys, etc.)
- Accidental secret commits are hard to fully undo (git history, clones)
- Need automated prevention at the git hook level

**Decision:**
- Use `pre-commit` framework (Python) with `gitleaks` hook for secret detection
- Hooks installed on both `pre-commit` and `pre-push` stages (double protection)
- `.gitleaks.toml` at repo root configures gitleaks with default rules + allowlist for `.env.example` and docs
- `.pre-commit-config.yaml` at repo root defines the hook

**Alternatives Considered:**
- GitHub secret scanning alone → Rejected: too late — secrets already pushed; need client-side prevention
- Husky (Node.js) + custom script → Rejected: `gitleaks` has comprehensive built-in rules for 100+ secret types
- `detect-secrets` (Yelp) → Considered: good alternative, but gitleaks is faster and has better pre-commit integration

**Consequences:**
- ✅ Secrets blocked before they enter git history
- ✅ Double layer: pre-commit + pre-push
- ✅ Zero false positives on current codebase (verified)
- ❌ Requires Python + `pre-commit` installed (documented in README)
- ❌ Each developer must run `pre-commit install` after cloning

---

### ADR-010: OpenAPI + Scalar for API Documentation with Auto-Generated Contract Markdown (2026-04-25)

**Context:**
- Need API documentation for the minimal API endpoints
- Swagger/Swashbuckle is deprecated for .NET 10; Microsoft recommends `Microsoft.AspNetCore.OpenApi`
- Want a persistent markdown record of all APIs that stays in sync automatically

**Decision:**
- Use `Microsoft.AspNetCore.OpenApi` (built-in .NET 10) for OpenAPI spec generation
- Use `Scalar.AspNetCore` for interactive API docs UI (modern replacement for Swagger UI)
- `ApiContractGenerator` hosted service auto-generates `docs/api-contract.md` from OpenAPI spec on startup (dev mode only)
- OpenAPI spec available at `/openapi/v1.json`; Scalar UI at `/scalar/v1`

**Alternatives Considered:**
- Swashbuckle/Swagger → Rejected: deprecated; not supported in .NET 10
- NSwag → Considered: heavier; built-in OpenAPI is simpler for our needs
- Manual markdown maintenance → Rejected: drifts out of sync with actual endpoints

**Consequences:**
- ✅ API docs always match running code — zero manual maintenance
- ✅ Scalar provides modern, interactive API exploration UI
- ✅ `api-contract.md` serves as version-controlled API reference
- ❌ 3-second startup delay for contract generation (dev only, non-blocking)

---

### ADR-011: Distributed User Settings — Each Module Owns Its Domain-Specific Preferences (2026-04-25)

**Context:**
- User settings span multiple bounded contexts: profile/auth (Identity), storefront follows (Catalog), wishlists (Cart), shipping preferences (Orders), payment methods (Payments)
- Need to decide: single `UserSettings` mega-entity in Identity, or distributed settings per module?
- Code will grow to 100K+ lines; wrong boundary now = painful refactoring later
- Must align with existing ADR-004 (schema-per-module) and module boundary rules

**Decision:**
- **Distributed ownership**: each module owns settings within its domain
  - **Identity**: User profile, UserAddress, UserPreferences (timezone/format), NotificationPreference, UserPrivacy, UserSession (Phase 2), UserTwoFactorAuth (Phase 2)
  - **Catalog**: UserFavoriteSeller (follows storefronts)
  - **Cart**: WishlistItem (saved products)
  - **Orders**: UserShippingPreference, OrderPreference (dispute resolution, notification delay)
  - **Payments**: PaymentMethod (Phase 2+)
- Cross-module reads via `IUserPreferencesReadService` and `INotificationPreferenceReadService` contracts in Core
- Settings UI is a single page (`/account/settings`) with 9 HTMX tabs — each tab calls its owning module's handler
- 1:1 preference entities (UserPreferences, NotificationPreference, UserPrivacy, etc.) are lazy-created with defaults on first access

**Alternatives Considered:**
- Single `UserSettings` entity in Identity with all preferences → Rejected: violates module boundaries (Orders/Catalog concerns in Identity schema); causes Identity module to grow into God module; every new feature requires Identity changes
- Settings microservice → Rejected: premature for Phase 1; adds network hop for every preference read
- Store all preferences in Redis → Rejected: non-durable; preferences are long-lived data that needs ACID guarantees

**Consequences:**
- ✅ Each module independently evolves its settings without touching other modules
- ✅ When modules split to microservices (Phase 3+), settings travel with their owning service — zero migration
- ✅ Settings page is just a UI composition layer (HTMX tabs) — no domain logic coupling
- ✅ Cross-module contracts are read-only snapshots — minimal coupling
- ❌ Settings page loads from multiple modules (mitigated: HTMX tabs load one at a time)
- ❌ Slightly more entities than a single mega-table (but each is small and focused)

---

### ADR-012: Automatic Auditing via Attributes — EF Interceptor + MediatR Behavior (2026-04-26)

**Context:**
- Admin portal needs to investigate user actions, data changes, login attempts, and security events
- Adding audit logging manually to each API/handler is error-prone and creates thousands of lines of boilerplate
- Need a foundation that works automatically — mark once, audit forever

**Decision:**
- New `MarketNest.Auditing` module with its own `auditing` schema (ADR-004 compliant)
- Two automatic capture points:
  1. **EF Core `AuditableInterceptor`**: entities marked `[Auditable]` are auto-logged on INSERT/UPDATE/DELETE with old/new value snapshots
  2. **MediatR `AuditBehavior<,>`**: commands marked `[Audited("EVENT_TYPE")]` are auto-logged after execution with success/failure status
- `IAuditService` contract in `Core/Contracts/` — Phase 1 writes to DB directly; Phase 3 swaps to `MessageBusAuditService` (RabbitMQ) with zero module code changes
- Login events recorded explicitly via `IAuditService.RecordLoginAsync()` in Identity module
- Admin queries: `GetAuditLogsQuery`, `GetLoginEventsQuery` with paged, filterable results
- Audit logs are append-only — no updates or deletes
- Audit failures never break the main request (catch + log)

**Alternatives Considered:**
- Manual audit logging in each handler → Rejected: thousands of boilerplate lines, easy to miss
- Third-party audit library (Audit.NET) → Rejected: adds external dependency for something simple; our pattern is more aligned with existing CQRS architecture
- Database triggers → Rejected: no actor/user context available at DB level; harder to maintain

**Consequences:**
- ✅ Zero per-API effort — add `[Auditable]` to entity or `[Audited]` to command, done
- ✅ Consistent audit format across all modules
- ✅ Transport-agnostic — same `IAuditService` interface for monolith and microservice
- ✅ Interceptor skips `AuditingDbContext` — no infinite recursion
- ❌ Interceptor adds ~2-5ms per SaveChanges (acceptable for Phase 1)
- ❌ EF interceptor captures data-level changes; MediatR behavior captures business intent — some actions may generate both (acceptable overlap)

---

### ADR-013: Background Job Management Foundation — Observable Timer and Batch Jobs (2026-04-25)

**Context:**
- MarketNest has many planned background jobs: reservation cleanup, order auto-confirmation, order auto-completion, payout batch processing, notification digests, wishlist cleanup, and future admin-triggered jobs.
- Admin users eventually need to operate jobs: view schedules, see execution status, inspect errors, retry failed executions, disable risky jobs, and register batch jobs.
- Implementing full job management too early would slow Phase 1, but not defining a shared foundation now risks each module building its own job pattern, causing operational complexity when the codebase grows to 100K+ lines.

**Decision:**
- Introduce a lightweight **Job Management foundation** in Phase 1:
  - All timer and batch jobs must register metadata through a shared `IJobRegistry`.
  - All executions must be recorded through `IJobExecutionStore`.
  - Jobs expose stable identifiers using `JobKey`.
  - Job execution records include status, timestamps, error details, retry relationship, trigger source, and owning module.
- Admin UI for job operations is deferred:
  - Phase 1: contracts, conventions, metadata, execution log storage.
  - Phase 2: admin read-only dashboard + retry failed execution + manual trigger for safe jobs.
  - Phase 3+: dynamic batch registration, queue-backed processing, distributed execution, and service split support.
- Timer jobs and batch jobs are treated differently:
  - **Timer jobs** are predefined scheduled jobs owned by modules.
  - **Batch jobs** are explicit one-off or queued operations, optionally created by admin users.
- Retry is allowed only for jobs marked `IsRetryable = true`.
- Dangerous jobs must be idempotent before being exposed to admin manual trigger or retry.

**Alternatives Considered:**
- Implement Hangfire dashboard immediately → Rejected for now: useful but adds operational surface area before core marketplace flows are complete.
- Let each module manage its own background job logs → Rejected: creates inconsistent retry, visibility, and audit behavior.
- Build custom full scheduler in Phase 1 → Rejected: too much complexity for the current phase.
- Ignore job observability until Phase 3 → Rejected: refactoring every existing job later would be expensive.

**Consequences:**
- ✅ Future admin job dashboard can be built without rewriting module jobs.
- ✅ Every job has consistent observability: schedule, status, duration, errors, and retry history.
- ✅ Jobs remain module-owned while operations are centralized.
- ✅ Supports future migration from in-process jobs to MassTransit / worker services.
- ❌ Adds small upfront design overhead in Phase 1.
- ❌ Requires discipline: all new background jobs must use the shared contracts.

---

### ADR-015: Voucher/Promotions Domain Design — Two-Axis Discount Model, Single Table (2026-04-27)

**Context:**
- MarketNest needs promotions for both platform-wide campaigns (Admin) and per-shop discounts (Seller)
- Discount types span percentage-off and fixed amounts; targets span product subtotal and shipping fees
- Need clear ownership of discount cost (who absorbs: Platform or Seller)

**Decision:**
- New `MarketNest.Promotions` module with `promotions` PostgreSQL schema
- **Two-axis model**: `VoucherDiscountType` (PercentageOff | FixedAmount) × `VoucherApplyFor` (ProductSubtotal | ShippingFee)
  - "Free shipping" = `PercentageOff 100%` on `ShippingFee` — no separate enum value needed
- **Single table** discriminated by `Scope` (Platform | Shop) — ~90% shared schema; avoids two-table join for "all valid vouchers"
- **Discount attribution**: Platform vouchers → Platform absorbs cost; Shop vouchers → Seller absorbs cost
- Shop voucher on ProductSubtotal reduces CommissionBase (seller bears full discount)
- Platform voucher never affects CommissionBase
- **Checkout**: max 1 Platform voucher + 1 Shop voucher per shop per checkout
- **Snapshot**: `AppliedVoucherSnapshot` embedded as JSONB on Order — cross-module, no DB FK to Promotions
- **Immutability**: after first `VoucherUsage`, core discount fields are locked

**Alternatives Considered:**
- Separate `PlatformVoucher` + `ShopVoucher` tables → Rejected: nearly identical schema; harder to query "all eligible vouchers"
- `FreeShipping` as a separate `VoucherDiscountType` → Rejected: conflates calculation method with target object (Single Responsibility violation)
- Discount in Orders module → Rejected: promotions is a distinct bounded context with its own lifecycle

**Consequences:**
- ✅ Expressive: any real-world discount modeled by two orthogonal axes
- ✅ Clear cost attribution for payout calculation
- ✅ Promotions is an independent module — extractable to microservice in Phase 3
- ❌ Checkout handler must coordinate with Promotions module (sync call via `IVoucherService`)

---

### ADR-016: Order Financial Calculation — Two-Perspective Model with Canonical Formula (2026-04-27)

**Context:**
- Existing `domain-and-business-rules.md` had `Total = Subtotal + ShippingFee - Discount` — insufficient with vouchers and payment surcharge
- Need a precise definition of what "total" means from buyer vs seller perspective
- Commission scope was unclear (was it on gross or net subtotal when vouchers apply?)

**Decision:**
- **Two-perspective model** (never mix):
  - **Buyer perspective**: `BuyerTotal = NetProductAmount + NetShippingFee + PaymentSurcharge`
  - **Seller perspective**: `NetAmount = CommissionBase - CommissionAmount - ShopShippingDiscount + GrossShippingFee`
- **PaymentSurcharge** introduced: buyer-facing surcharge for card payments (e.g. 2%), Admin-configured per PaymentMethod — displayed as a separate checkout line
- **CommissionBase** depends on voucher type: `SellerSubtotal - ShopProductDiscount` (shop voucher on products); `SellerSubtotal` (platform voucher — platform absorbs)
- **Shipping model**: Platform-mediated (Option A) — platform collects `GrossShippingFee`, remits to seller minus `ShopShippingDiscount`
- All financial components **computed once at checkout and stored as snapshots** — no recalculation (same pattern as `OrderLine.UnitPrice`)
- `Payment.Amount` renamed to `Payment.ChargedAmount`; `Payout` separated from `Payment` as its own aggregate
- `Gateway cost` (e.g. 2.9% + $0.30) is **internal platform cost** — separate from buyer-facing `PaymentSurcharge`

**Alternatives Considered:**
- Keep `Total = Subtotal + ShippingFee - Discount` → Rejected: doesn't account for payment surcharge or multi-voucher breakdown
- Surcharge absorbed into product price → Rejected: misleading to buyer; complicates accounting
- CommissionBase always on gross subtotal → Rejected: seller would pay commission on money they gave away via shop voucher
- Option B shipping (buyer pays carrier directly) → Rejected: complex in Phase 1; platform-mediated is simpler

**Consequences:**
- ✅ Precise, auditable: every financial component has a named field with a snapshot
- ✅ No ambiguity: BuyerTotal vs SellerNetPayout are clearly separated
- ✅ Supports any future fee types (just add a new snapshot field)
- ❌ More fields on Order aggregate (but all are snapshot fields, not computed live)
- ❌ `SellerNetPayout` can theoretically go negative if commission + shop voucher > subtotal — requires alerting (F6)

---

### ADR-014: [LoggerMessage] Source-Generated Delegates as Mandatory Logging Pattern (2026-04-26)

**Status**: Implemented ✅ (2026-04-27)

**Context:**
- `IAppLogger<T>` previously used `params object?[]` overloads → CA1848 + CA2254 suppressed via `#pragma`
- Runtime cost: template parsed every call, value types boxed, zero allocation skip when level disabled
- No EventId on any log statement → could not filter precisely in Seq
- 8 domain modules and 19 pages had zero logging coverage

**Decision:**
- All production logging must use `[LoggerMessage]` source-generated delegates (CA1848 compliant)
- `IAppLogger<T>` extended to implement `ILogger` (explicit `ILogger.Log<TState>` via `inner.Log()` — not extension methods, so no CA1848)
- `.Info()` / `.Warn()` / `.Error()` methods stripped from `IAppLogger<T>` — it is now a DI marker interface; `AppLogger<T>` retains only 3 explicit ILogger members; `#pragma` removed
- Each module owns a block of 1000 EventIds — registry lives in `MarketNest.Base.Infrastructure/Logging/LogEventId.cs`
- `private static partial class Log` nested inside each class; outer class must be `partial`
- Exception param always last, never in message template

**Alternatives Considered:**
- Keep `#pragma` suppression → Rejected: hides real issues; CA1848 exists for good reason
- Expose `ILogger InnerLogger { get; }` on IAppLogger → Rejected: call sites become `_logger.InnerLogger` — noisier, no benefit
- Replace `IAppLogger<T>` with `ILogger<T>` everywhere → Rejected: large scope, breaks DI conventions already in use

**Consequences (achieved):**
- ✅ Zero allocation for disabled log levels — hot paths unaffected
- ✅ Compile-time type safety: wrong param count/type → build error, not runtime bug
- ✅ Stable EventIds per module → precise Seq filter (`EventId = 2652`)
- ✅ `#pragma warning disable CA1848, CA2254` eliminated from all production code
- ✅ 50+ files migrated; 19 pages added first-time observability
- ❌ Requires `partial` on every class that logs
- ❌ More boilerplate per file (mitigated by nested `Log` class keeping it local)

---
