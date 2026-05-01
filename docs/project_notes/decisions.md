# Architectural Decisions

Architectural Decision Records (ADRs) for MarketNest. Number sequentially. Keep all entries — they provide historical context.

**Review cadence**: Review quarterly. Mark outdated decisions as `**Status**: Superseded by ADR-XXX` — **never delete** old ADRs. Future developers need the "why" behind legacy code.

**Keep entries concise**: Each ADR should be scannable in 30 seconds. Link to external docs for lengthy analysis.

**When this file exceeds ~20 entries**: Add a Table of Contents at the top.

## Table of Contents

| ADR | Title | Date |
|-----|-------|------|
| ADR-001 | Modular Monolith → Microservices → Kubernetes Phased Architecture | 2026-04-25 |
| ADR-002 | Razor Pages + HTMX + Alpine.js (No SPA Framework) | 2026-04-25 |
| ADR-003 | Result<T, Error> — No Exceptions for Business Failures | 2026-04-25 |
| ADR-004 | EF Core with Schema-Per-Module Boundary Enforcement | 2026-04-25 |
| ADR-005 | No Magic Strings / Magic Numbers | 2026-04-25 |
| ADR-006 | System Tables in `public` Schema, Module Tables in Named Schemas | 2026-04-25 |
| ADR-007 | DDD Property Accessor Convention | 2026-04-25 |
| ADR-008 | Integration Event Infrastructure — Transport-Agnostic Event Bus | 2026-04-25 |
| ADR-009 | Pre-commit Secret Detection with Gitleaks | 2026-04-25 |
| ADR-010 | OpenAPI + Scalar for API Documentation | 2026-04-25 |
| ADR-011 | Distributed User Settings — Each Module Owns Its Domain-Specific Preferences | 2026-04-25 |
| ADR-012 | Automatic Auditing via Attributes | 2026-04-26 |
| ADR-013 | Background Job Management Foundation | 2026-04-25 |
| ADR-014 | [LoggerMessage] Source-Generated Delegates as Mandatory Logging Pattern | 2026-04-26 |
| ADR-015 | Voucher/Promotions Domain Design — Two-Axis Discount Model | 2026-04-27 |
| ADR-016 | Order Financial Calculation — Two-Perspective Model | 2026-04-27 |
| ADR-020 | Consolidate Agent Guidelines into a Single Canonical File | 2026-04-26 |
| ADR-021 | Three-Tier Configuration Model | 2026-04-28 |
| ADR-022 | `ReferenceData` Base Entity in `Base.Domain` | 2026-04-28 |
| ADR-023 | EF Core DDD Property Access Convention — `ApplyDddPropertyAccessConventions()` | 2026-04-28 |
| ADR-024 | Sale Price as Inline Fields on ProductVariant — Option A | 2026-04-29 |
| ADR-025 | Canonical BaseQuery / BaseRepository in Base.Infrastructure with Module-Local Thin Wrappers | 2026-04-29 |
| ADR-026 | SLA Requirements Formalized as First-Class Project Concern | 2026-04-29 |
| ADR-027 | Unit of Work + [Transaction] Attribute — Domain Event Lifecycle & Transaction Management | 2026-04-29 |
| ADR-028 | IRuntimeContext — Unified Ambient Request Context | 2026-04-29 |
| ADR-029 | Four-Layer Caching Strategy & Cross-Module Service Contracts | 2026-04-29 |
| ADR-030 | Application Constants vs Configuration — Immutable Rules in AppConstants, Environment-Specific Settings in appsettings.json | 2026-04-29 |
| ADR-031 | Two-Connection-String Pattern — DefaultConnection (write) + ReadConnection (read-replica fallback) | 2026-04-30 |
| ADR-032 | PgQueryBuilder — Safe Raw PostgreSQL Query Generation Utility | 2026-04-30 |
| ADR-033 | Expand LogEventId from 1,000 to 10,000 per module | 2026-04-30 |
| ADR-034 | Notifications Module — Template-Based Dispatch with Email + In-App Channels | 2026-04-30 |
| ADR-035 | SharedViewPaths — Centralized Razor Partial Path Constants | 2026-04-30 |
| ADR-036 | Rich Text Editor — Trix with Server-Side HTML Sanitization | 2026-04-30 |
| ADR-037 | Excel Import/Export — ClosedXML + IExcelService + IAntivirusScanner | 2026-04-30 |
| ADR-038 | I18N Service — II18NService Wrapper + I18NKeys Constants | 2026-04-30 |
| ADR-039 | Nullable Management — Business Decision Model with `#pragma` on EF Constructors | 2026-04-30 |
| ADR-040 | Period-Scoped PostgreSQL Sequences for Running Numbers | 2026-04-30 |
| ADR-041 | Optimistic Concurrency Control via IConcurrencyAware + UpdateToken | 2026-04-30 |
| ADR-042 | MN019/MN020 — Handler Entity Return & QueryHandler Select-Projection Analyzer Rules | 2026-04-30 |
| ADR-043 | Announcement Feature Foundation — Admin-managed site-wide announcements with scheduling | 2026-05-01 |

> **Note**: ADR-017, ADR-018, ADR-019 are reserved/not yet assigned.

---

### ADR-025: Canonical BaseQuery / BaseRepository in Base.Infrastructure with Module-Local Thin Wrappers (2026-04-29)

**Context:**
- `BaseQuery<TEntity,TKey,TContext>` and `BaseRepository<TEntity,TKey,TContext>` were previously only in the Admin module, forcing other modules to duplicate the same boilerplate.
- `IBaseQuery<TEntity,TKey>` was already canonical in `Base.Common`, but had no shared concrete implementation.
- Need a single authoritative implementation that all modules inherit without duplicating EF Core logic.

**Decision:**
- `BaseQuery<TEntity, TKey, TContext>` (implements `IBaseQuery<TEntity,TKey>`) lives in `Base.Infrastructure` (namespace `MarketNest.Base.Infrastructure`). Provides Count, Any, GetByKey, FindByKey, FirstOrDefault, List, GetPagedList, GetQueryable.
- `BaseRepository<TEntity, TKey, TContext>` (implements `IBaseRepository<TEntity,TKey>`) lives in `Base.Infrastructure`. Provides GetByKey, FindByKey, Exists, Add, Update, Remove, SaveChanges.
- `IBaseRepository<TEntity, TKey>` lives in `Base.Infrastructure`.
- Each module declares a 2-line local alias that pins the module's own `DbContext` type:
  ```csharp
  // read side
  public abstract class BaseQuery<TEntity, TKey>(ModuleReadDbContext db)
      : BaseQuery<TEntity, TKey, ModuleReadDbContext>(db);
  // write side
  public abstract class BaseRepository<TEntity, TKey>(ModuleDbContext db)
      : BaseRepository<TEntity, TKey, ModuleDbContext>(db);
  ```
- `DependencyInjection.cs` extracted to all remaining modules (`Auditing`, `Promotions`) following existing `Admin` / `Orders` pattern (`AddXxxModule()`).

**Alternatives Considered:**
- Keep per-module copies → Rejected: duplication; divergence risk when base logic changes.
- Source generators / T4 templates → Rejected: unnecessary complexity; inheritance is simpler and type-safe.

**Consequences:**
- ✅ Single implementation to maintain; all modules pick up bug fixes automatically.
- ✅ New modules only need a 2-line thin wrapper — no boilerplate to copy.
- ✅ Consistent query/repository API surface across all modules.
- ❌ Module `.csproj` files must reference `Base.Infrastructure` (already a common dep).

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
- Each module owns a block of 10,000 EventIds — registry lives in `MarketNest.Base.Infrastructure/Logging/LogEventId.cs`
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

### ADR-015: Voucher/Promotions Domain Design — Two-Axis Discount Model (2026-04-27)

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

### ADR-020: Consolidate Agent Guidelines into a Single Canonical File (2026-04-26)

**Context:**
- Multiple overlapping agent-facing rule documents existed: `AGENTS.md`, `CLAUDE.md`, `agents/rules/*.md`
- Risk of divergence when different AI assistants (Copilot, Claude, Gemini) read different files
- Duplication meant rule updates had to be applied in 3+ places

**Decision:**
- Single canonical agent guidelines file at `agents/GUIDELINES.md`
- `AGENTS.md` and `CLAUDE.md` reference `agents/GUIDELINES.md` rather than duplicating rules
- Original `agents/rules/*.md` archived under `agents/rules/archive/`; replaced with short pointers
- ADR stored at `docs/adr/ADR-020-canonical-agent-guidelines.md`

**Alternatives Considered:**
- Keep multiple files in sync manually → Rejected: proven to drift already
- Pick one file (CLAUDE.md) as canonical → Rejected: Copilot/Gemini don't read CLAUDE.md

**Consequences:**
- ✅ Single source of truth reduces maintenance and inconsistent agent behavior
- ✅ Any AI tool can find the same rules from `agents/GUIDELINES.md`
- ❌ Adds one extra file to read; mitigated by the pointer links in AGENTS.md/CLAUDE.md

---

### ADR-021: Three-Tier Configuration Model (2026-04-28)

**Context:**
- Need a unified way to manage lookup data (dropdowns), runtime business rules, and technical settings.
- Admin module must not become a "God Module" with direct DB access to other modules.

**Decision:**
- **Tier 1 — Reference Data**: Country, Gender, PhoneCountryCode, Nationality, ProductCategory. Owned by Admin module (`admin` schema), seeded from embedded JSON. All tables explicitly mapped to `public` schema via EF config. Consumed via `IReferenceDataReadService` contract (in `Base.Common`). Redis TTL: 24h.
- **Tier 2 — Business Configuration**: OrderPolicyConfig, CommissionPolicy, StorefrontPolicyConfig, ReviewPolicyConfig. *Owned by the module that uses the config* (Orders, Payments, Catalog, Reviews). Admin writes via `IXxxConfigWriter` contracts in `Base.Common/Contracts/Config/`. Redis TTL: 1h.
- **Tier 3 — System Configuration**: PlatformOptions, ValidationOptions, SecurityOptions. Strongly-typed Options bound from `appsettings.json`. No DB, no UI — change requires redeployment.

**Alternatives Considered:**
- Single `master_data` table in Admin schema → rejected: blurs module boundaries, Admin becomes God Module (ADR-004 violation)
- Business Config living in Admin schema → rejected: domain knowledge (e.g. `OrderWindowHours`) would live in the wrong module
- Redis as source-of-truth for config → rejected: data loss on Redis restart, no fallback

**Consequences:**
- ✅ Clear ownership: each tier has a single owner (Admin / owning module / Infrastructure)
- ✅ Admin never references other modules' internals — uses only contract interfaces
- ✅ Reference data is globally queryable via `IReferenceDataReadService` without cross-module DB joins
- ✅ Tier 2 config is DB-persistent with Redis caching (survives restarts)
- ❌ More files per config type (entity + writer service + contract interface)
- ❌ Tier 2 implementations for Catalog/Reviews are in-memory stubs in Phase 1 (Phase 2 will add DB-backed implementations)

---

### ADR-022: `ReferenceData` Base Entity in `Base.Domain` (2026-04-28)

**Context:**
- Reference data entities (Country, Gender, etc.) live in `Admin.Domain` and extend a shared base.
- The base class needs to inherit from `Entity<int>` which is in `Base.Domain`.

**Decision:**
- `ReferenceData` abstract base class placed in `Base.Domain/ReferenceData/ReferenceData.cs`.
- Concrete types (Country, Gender, etc.) placed in `Admin.Domain/Modules/ReferenceData/`.
- EF Core configurations placed in `Admin.Infrastructure/Persistence/Configurations/`.
- Abstract `ReferenceDataConfiguration<T>` base EF config placed in `Admin.Infrastructure`.
- Uses `ValueGeneratedOnAdd()` instead of Npgsql-specific `UseIdentityColumn()` to keep Admin module free of Npgsql dependency.

**Consequences:**
- ✅ Concrete types stay in Admin (owner module); base stays in shared package
- ✅ Admin module doesn't need Npgsql package reference
- ❌ Base.Domain now contains a domain concept that is Admin-specific but needed across modules for DTO mapping — acceptable trade-off

---

### ADR-023: EF Core DDD Property Access Convention — `ApplyDddPropertyAccessConventions()` (2026-04-28)

**Context:**
- ADR-007 mandates `{ get; private set; }` on all entity/aggregate properties to protect invariants.
- EF Core needs to materialize entities from database rows, raising concern: "Does EF Core require public setters?"
- EF Core already supports `{ get; private set; }` natively — it uses the compiler-generated backing field (or reflection) to set property values. No `{ get; set; }` is needed.
- However, **collection navigation properties** exposed as `IReadOnlyList<T>` with an explicit private backing field (e.g., `private readonly List<T> _items`) need `PropertyAccessMode.Field` so EF Core populates the backing field directly instead of trying to use the (non-existent) property setter.

**Decision:**
- Created `DddModelBuilderExtensions.ApplyDddPropertyAccessConventions()` in `MarketNest.Base.Infrastructure/Persistence/`.
- The extension method:
  1. Sets model-level `PropertyAccessMode.PreferField` (explicit, matches EF Core default, documents DDD intent).
  2. Auto-detects collection navigations with an explicit `_camelCase` backing field and sets `PropertyAccessMode.Field` on those navigations.
- All module `DbContext.OnModelCreating()` calls `modelBuilder.ApplyDddPropertyAccessConventions()` after `ApplyConfigurationsFromAssembly()`.
- Collection navigation pattern standardized: always use `private readonly List<T> _items = [];` with `public IReadOnlyList<T> Items => _items.AsReadOnly();` (never auto-property `IReadOnlyList<T> { get; private set; }`).

**Alternatives Considered:**
- Manual `UsePropertyAccessMode(PropertyAccessMode.Field)` per navigation → Rejected: error-prone, easy to forget in new entities.
- `{ get; set; }` on entities → Rejected: violates ADR-007, breaks DDD invariant protection.
- No convention, rely on EF Core defaults → Rejected: implicit behavior is fragile for collection navigations with explicit backing fields.

**Consequences:**
- ✅ `{ get; private set; }` on scalar properties works with zero extra configuration — EF Core handles it natively.
- ✅ Collection navigations with explicit backing fields are auto-detected and configured correctly.
- ✅ Single place to maintain the convention — new modules just call `ApplyDddPropertyAccessConventions()`.
- ✅ No changes needed to entity designs — ADR-007 accessors are fully compatible with EF Core.
- ❌ Naming convention dependency: backing field must follow `_camelCase` for `PascalCase` property name.

---

### ADR-024: Sale Price as Inline Fields on ProductVariant — Option A (2026-04-29)

**Context:**
- Need to support time-limited sale prices on individual product variants.
- Two candidate designs: (A) three inline fields (`sale_price`, `sale_start`, `sale_end`) directly on the variant row, or (B) a separate `VariantPricePromotion` entity with a FK.

**Decision:** Option A — inline fields on `ProductVariant`.

**Rationale:**
- Phase 1: one variant = one active sale at a time — no scheduling queue needed.
- `EffectivePrice` queries require no extra JOIN: `WHERE sale_end > NOW()` is a single-table predicate.
- Consistent with Shopify/WooCommerce/Lazada design; familiar to domain experts.
- Checkout snapshot is trivial: call `variant.EffectivePrice()` — no eager-load of a child collection.

**Consequences:**
- ✅ Simple reads, simple writes, simple EF configuration.
- ✅ DB CHECK constraints enforce atomicity of all three fields (invariant S5).
- ✅ Background job (`ExpireSalesJob`, 5-min schedule) cleans up expired sales and raises domain event.
- ❌ No overlapping/scheduled multi-promotion queue (Phase 2 concern — migrate to Option B then if needed).
- ❌ No per-sale price history audit trail (mitigated by `[Auditable]` on entity and domain events).
- **Phase 2 migration path**: Add `VariantPricePromotion` entity, migrate active sale fields → first row, keep `EffectivePrice()` API stable.

---

### ADR-026: SLA Requirements Formalized as First-Class Project Concern (2026-04-29)

**Context:**
- MarketNest marketplace processes real financial transactions (orders, payouts, commissions). Without explicit SLA thresholds, slow requests and financial drift can go undetected.
- Business-critical invariants (no oversell, commission accuracy, payment reconciliation) were documented in `domain-and-business-rules.md` but had no corresponding runtime enforcement infrastructure.

**Decision:**
- Formalize a four-dimension SLA framework: Availability, Performance, Business Correctness, Data Integrity.
- Capture all thresholds as first-class constants (`SlaConstants` in `Base.Common`) — no magic numbers.
- Implement Phase 1 foundation: `PerformanceBehavior` (MediatR), `FinancialReconciliationJob` stub (Payments), and `SlaConstants`.
- Full doc lives at `docs/sla-requirements.md`.

**Consequences:**
- ✅ All SLA thresholds are typed and searchable — enforced by MN005 no-magic-number analyzer.
- ✅ `PerformanceBehavior` logs every slow/critical request via Seq from day one.
- ✅ `FinancialReconciliationJob` skeleton is registered and scheduled; full logic unlocks once Order + Payment aggregates are complete.
- ✅ Cross-reference table aligns SLA checks with existing domain invariants (I1, P2, §10.2).
- ❌ P95 statistical tracking deferred to Phase 2 (requires OTEL histogram → Prometheus).
- ❌ `/admin/sla` dashboard deferred to Phase 2.
- **Phase 2 path**: Emit OTEL histogram metrics from `PerformanceBehavior`; wire Grafana dashboards; migrate `SlaConstants` thresholds to `AdminConfig` DB backing (ADR-021).

---

### ADR-028: IRuntimeContext — Unified Ambient Request Context (2026-04-29)

**Context:**
- Every handler, middleware, and page was injecting `ICurrentUserService` separately to get `UserId`.
- `CorrelationId` was read from `HttpContext.TraceIdentifier` at each call site.
- Background jobs had no consistent way to carry user/correlation info.
- Tests required mocking multiple services instead of one.

**Decision:**
- `IRuntimeContext` is the single injection point for: `CorrelationId`, `RequestId`, `CurrentUser` (Id, Name, Email, Role), `StartedAt`, `ElapsedMs`, `ClientIp`, `UserAgent`, `HttpMethod`, `RequestPath`.
- `ICurrentUser` contract in `Base.Common`: `Id?`, `Name?`, `Email?`, `Role?`, `IsAuthenticated`, `RequireId()` (throws `UnauthorizedException`), `IdOrNull`.
- `RuntimeExecutionContext` enum: `HttpRequest | BackgroundJob | Test`.
- `UnauthorizedException` added to `Base.Common` — thrown by `RequireId()` when anonymous.
- `HttpRuntimeContext` (Scoped, mutable) populated once by `RuntimeContextMiddleware` after `UseAuthentication()`.
- `BackgroundJobRuntimeContext` (static factory): `ForSystemJob(jobKey)` and `ForAdminJob(jobKey, adminId)`.
- `TestRuntimeContext` (UnitTests): `AsAnonymous()`, `AsBuyer()`, `AsSeller()`, `AsAdmin()` builder helpers.
- `RuntimeContextMiddleware`: enriches Serilog `LogContext` (CorrelationId, UserId, UserRole) + OTel Activity tags + echoes `X-Correlation-ID` response header.
- LogEventIds `1094` (RequestStart) and `1095` (RequestEnd) added.

**Alternatives Considered:**
- Keep `ICurrentUserService` → Rejected: scattered injection, no correlation/timing, no background job support.
- Use `IHttpContextAccessor` directly in handlers → Rejected: couples application layer to HTTP; broken in jobs/tests.

**Consequences:**
- ✅ Single inject replaces `ICurrentUserService` + `HttpContext.TraceIdentifier` everywhere.
- ✅ Every log line gets CorrelationId / UserId automatically via Serilog enrichment.
- ✅ Background jobs get a consistent context via static factories.
- ✅ Tests need one line: `TestRuntimeContext.AsSeller()`.
- ✅ OTel Activity tagged for distributed tracing readiness (Phase 2).
- ❌ Migration: existing code using `ICurrentUserService` should be updated to `IRuntimeContext.CurrentUser` (done incrementally).

---

### ADR-027: Unit of Work + [Transaction] Attribute — Domain Event Lifecycle & Transaction Management (2026-04-29)

**Status**: Updated 2026-04-29. Significant pattern change: **filters now own transaction lifecycle; handlers only mutate entities.**

**Context (original):**
- Command handlers were calling `dbContext.SaveChangesAsync()` directly, bypassing domain event dispatch and transaction control.
- Domain events had no ordering guarantee: all events were post-commit which prevented atomic side effects (e.g., reserving inventory in the same TX as placing an order).
- Write operations in Razor Pages and API controllers had no automatic transaction boundary.

**Decision (revised 2026-04-29):**

The Unit of Work pattern is split into two distinct use cases, each with its own transaction management strategy:

#### HTTP Handlers (via filters — automatic)
- **`RazorPageTransactionFilter`** and **`TransactionActionFilter`** (global) own the full transaction lifecycle.
- **New `IUnitOfWork` methods added:**
  - `BeginTransactionAsync(IsolationLevel, CancellationToken)` — filter opens DB transactions on all module contexts
  - `CommitAsync(CancellationToken)` — dispatcher calls this ONCE after handler returns; method: dispatch pre-commit events → `SaveChangesAsync` on all contexts
  - `CommitTransactionAsync(CancellationToken)` — filter calls this to commit DB transactions (point of no return)
  - `RollbackAsync(CancellationToken)` — filter calls on exception; clears post-commit event queue + rolls back DB transactions
  - `IAsyncDisposable DisposeAsync()` — filter calls on finally to clean up transaction objects
- **Command handlers DO NOT inject `IUnitOfWork`** — only repositories. They mutate entities and return. Filter handles commit automatically.
- **Benefits**: Handlers can't forget to commit. No silent data loss if handler raises an exception after mutations.

#### Background Jobs (explicit management)
- Background jobs run **outside the HTTP filter** — they must explicitly manage transactions via the new `IUnitOfWork` methods.
- Pattern: `try { BeginTransaction → CommitAsync → CommitTransactionAsync → DispatchPostCommitEventsAsync } catch RollbackAsync finally DisposeAsync`
- This ensures **atomicity**: if an exception occurs after `CommitAsync`, the entire transaction is rolled back (including all entity changes).

#### Domain Event Processing

- **`IPreCommitDomainEvent`** (marker, `Base.Domain`): domain events implementing this run INSIDE the open transaction before `SaveChanges`. Used for atomic side effects (e.g., inventory reservation).
- All other domain events are post-commit (`IDomainEvent` default).
- **`IHasDomainEvents`** (non-generic interface, `Base.Domain`): added to `Entity<TKey>` so `UnitOfWork` can scan `ChangeTracker` without knowing the key type.

#### Transaction Attributes

- **`[Transaction]` / `[NoTransaction]`** (in `Base.Common`): control transaction wrapping on handlers.
- **`ReadApiV1ControllerBase`** / **`WriteApiV1ControllerBase`** (in `Base.Api`): write controllers carry `[Transaction]` at class level, enforcing transactions automatically.
- Both filters open transactions on ALL module DbContexts before the handler runs, then commit/rollback all after the handler returns.

**Consequences:**
- ✅ **Handlers simplified**: no UoW injection, no commit calls — pure domain mutation code.
- ✅ **Automatic transactionality**: forget to call commit → impossible; filter handles it.
- ✅ **Full transaction control**: background jobs manage transaction lifecycle explicitly vs HTTP handlers (automatic via filter).
- ✅ **Pre-commit events guarantee atomicity**: inventory reservation happens in the same transaction as order placement.
- ✅ **Phase 3 path**: swap `DispatchPostCommitEventsAsync` to write to Outbox table — no handler code changes.
- ⚠️ **Opening transactions on all module DbContexts** has overhead per write request (mitigated by PostgreSQL connection pooling + short transactions).
- ⚠️ **True distributed atomicity** across modules requires saga/outbox (Phase 3). Phase 1 relies on module boundary rule: each command touches one module's DbContext.

---

### ADR-029: Four-Layer Caching Strategy & Cross-Module Service Contracts (2026-04-29)

**Context:**
- MarketNest needed a comprehensive caching approach: static assets, server-rendered HTML, application-level Redis, and cross-module data access.
- Static files (`asp-append-version`) were only applied to CSS, not JS. No `Cache-Control` headers were set.
- HTMX partial responses had no `no-store` protection — browsers could cache stale partials.
- `OutputCache` was not configured for anonymous Razor Pages.
- `CacheKeys` existed but only covered Tier 1 (reference data) and Tier 2 (business config) — no module-specific keys for Catalog, Cart, Payments, Identity.
- Cross-module communication question: gRPC vs BFF vs service contracts for data that's too large to cache.

**Decision:**
1. **Layer 1 (Static assets)**: `asp-append-version="true"` on all local CSS + JS tags. `StaticFileOptions` with `Cache-Control: immutable` for fingerprinted files, `max-age=86400` for media, `no-cache` for everything else.
2. **Layer 1b (HTMX)**: `HtmxNoCacheMiddleware` forces `no-store` on all `HX-Request` responses.
3. **Layer 2 (OutputCache)**: Three named policies (`AnonymousPublic` 60s, `Storefront` 5m, `ProductDetail` 2m) — anonymous users only. Constants in `CachePolicies`.
4. **Layer 3 (Redis)**: Expanded `CacheKeys` with `Catalog`, `Cart`, `Payments`, `Identity`, `Admin` nested classes and new TTL presets (`VeryShort` 30s, `QuickExpiry` 1m, `VeryLong` 6h).
5. **Layer 4 (Cross-module)**: **Service contracts via interfaces** (in-process DI injection, not gRPC or BFF). Same pattern as existing `IReferenceDataReadService`, `IStorefrontReadService`. gRPC/BFF deferred to Phase 3 when modules become separate services.
6. **Redis safety**: Upgraded `RemoveByPrefixAsync` from `KEYS` to `SCAN`-based cursor iteration with batched deletion.

**Alternatives rejected:**
- gRPC between modules: over-engineering for monolith (serialization overhead, proto files, transport complexity for zero benefit)
- BFF layer: adds indirection with no value when all modules are in-process
- `ISharedCacheService` wrapper: unnecessary abstraction over `ICacheService` — key namespace convention prevents collisions

**Trade-offs:**
- ✅ Four layers cover the full request lifecycle from browser to DB
- ✅ In-process service contracts are zero-cost and migrate to gRPC in Phase 3 via DI swap
- ✅ SCAN-based prefix deletion is production-safe
- ⚠️ OutputCache uses in-memory store (Phase 2: swap to Redis-backed store)
- ⚠️ No cache stampede prevention yet (Phase 2)

**References:** `docs/caching-strategy.md`

---

### ADR-031: Two-Connection-String Pattern — DefaultConnection (write) + ReadConnection (read-replica fallback) (2026-04-30)

**Context:**
- All module DbContexts (write side and read side) used a single `DefaultConnection`.
- Question raised: should separate connection strings be created per module (`AuditConnection`, `NotificationConnection`) to ease future microservice extraction?
- Phase 2 read-replica scaling is a real concern; microservice extraction is a Phase 3 concern.

**Decision:**
- **Two connection strings only** — no per-module connection strings:
  - `DefaultConnection` — used by all write-side DbContexts (and read-side when `ReadConnection` is absent).
  - `ReadConnection` — **optional**; empty/absent in Phase 1 (falls back to `DefaultConnection`). Phase 2: set to a PostgreSQL read replica to route all `ReadDbContext` queries without any code change.
- Per-module extras (`AuditConnection`, `NotificationConnection`) are explicitly **rejected** — see "Alternatives Considered".
- Each module's `DependencyInjection.cs` resolves read connection as:
  ```csharp
  string readConnection = configuration.GetConnectionString("ReadConnection")
      is { Length: > 0 } rc ? rc : writeConnection;
  ```

**Alternatives Considered:**
- Per-module connection strings (`AuditConnection`, `NotificationConnection`, …) → Rejected:
  - When extracting a module to microservice (Phase 3), changing one connection string name is 1% of the total work. Schema isolation (ADR-004) is the real enabler.
  - Adds 6+ extra config entries (appsettings, .env, docker-compose) with zero Phase 1 operational benefit.
  - Violates "Simplicity First" — premature complexity for a benefit that materialises only at phase boundary refactoring.
- Single `DefaultConnection` for everything (status quo) → Rejected: misses Phase 2 read-replica opportunity. `ReadConnection` fallback has concrete value (zero code changes when a replica is added).

**Consequences:**
- ✅ Zero config noise in Phase 1 — `ReadConnection` is empty, falls back automatically.
- ✅ Phase 2 read-replica routing: set `ReadConnection` once → all module ReadDbContexts switched, zero code changes.
- ✅ Config stays clean — appsettings has exactly 2 connection string entries forever.
- ❌ Module connection string isolation requires explicit work at Phase 3 extraction boundary (but that's intentional and unavoidable).

---

### ADR-032: PgQueryBuilder — Safe Raw PostgreSQL Query Generation Utility (2026-04-30)

**Context:**
- EF Core covers 95%+ of data access needs. However, some edge cases require raw SQL: complex multi-schema joins, DDL commands (`CREATE SEQUENCE`, `CREATE INDEX`, `CREATE SCHEMA`), PostgreSQL-specific features (`advisory locks`, `LISTEN/NOTIFY`), or bulk operations bypassing the Change Tracker.
- Writing raw SQL strings directly risks SQL injection, especially when identifier names (table, column, schema) are dynamic.
- Need a safe, parameterized query builder that's always available but clearly positioned as a last-resort escape hatch.

**Decision:**
- `PgQueryBuilder` static utility class in `Base.Infrastructure/Persistence/PgQueryBuilder.cs` (namespace `MarketNest.Base.Infrastructure`).
- All value interpolation via positional parameters (`$1`, `$2`, …) — prevents SQL injection.
- Identifier quoting (`"schema"."table"`) with double-quote escaping.
- `RawSqlFragment` for trusted, developer-controlled SQL (column names, keywords) — **never user input**.
- Builders: `Query` (interpolated), `Select`, `Insert`, `InsertMany`, `Update`, `Delete`, `Upsert`, `InClause`, `NotInClause`, `Combine` (re-indexes parameters), `EscapeLike`.
- `ToDebugString` for dev logging only — output must never be executed.
- Uses `[GeneratedRegex]` source-generated regexes (compile-time, zero allocation).
- `PgQuery` result type is a `sealed record` (immutable, per project convention).

**Alternatives Considered:**
- Dapper → Rejected: adds another ORM library; EF Core + raw Npgsql is sufficient for edge cases.
- String concatenation with manual escaping → Rejected: error-prone, SQL injection risk.
- `FormattableString` with EF Core `FromSqlInterpolated` → Considered: works for queries returning entities, but doesn't help with DDL, cross-schema joins returning DTOs, or non-EF Npgsql commands.

**Consequences:**
- ✅ SQL injection prevention for all raw query use cases — parameterized by default.
- ✅ Identifier quoting prevents injection via dynamic table/column names.
- ✅ Available to all modules via `Base.Infrastructure` reference (already a common dep).
- ✅ Zero runtime overhead from source-generated regex.
- ✅ Clear escape hatch positioning — EF Core remains the primary data access tool.
- ❌ Developers must remember: `Raw()` and `IdentifierRaw()` bypass parameterization — only for trusted input.
- ❌ No query validation — generated SQL is not checked against the database schema at compile time.

---

### ADR-033 — Expand LogEventId from 1,000 to 10,000 per module

**Date**: 2026-04-30  
**Status**: Accepted  
**Supersedes**: Part of ADR-014 (EventId allocation only — rest of ADR-014 unchanged)

**Context:**
- Original allocation of 1,000 IDs per module (ADR-014) was too small — only 400 slots for Application layer.
- As modules grow with more handlers, pages, and background jobs, risk of collision or exhaustion was high.
- Separate Start/Success/Failed EventIds per operation (not grouped) is the correct pattern for Seq filtering and alerting — this requires ~3–4 IDs per use case.

**Decision:**
- Expand each module's EventId block from 1,000 to 10,000.
- New sub-allocation: X0000–X1999 (Infrastructure), X2000–X5999 (Application), X6000–X7999 (Web Pages), X8000–X9999 (Reserved).
- Keep separate EventIds for Start/Success/Failed — do NOT group into single ID. Use `CorrelationId` from `IRuntimeContext` for request tracing instead.

**Module ranges:**
- Infrastructure/Middleware: 10000–19999
- Identity: 20000–29999
- Catalog: 30000–39999
- Cart: 40000–49999
- Orders: 50000–59999
- Payments: 60000–69999
- Reviews: 70000–79999
- Disputes: 80000–89999
- Notifications: 90000–99999
- Admin: 100000–109999
- Auditing: 110000–119999
- Background Jobs: 120000–129999
- Web/Global Pages: 130000–139999
- Promotions: 140000–149999

**Trade-offs:**
- ✅ 10x headroom per module — no risk of exhaustion for Phase 1–4
- ✅ Separate EventIds enable precise filtering and alerting in Seq
- ✅ Easy mental model: module number × 10000
- ❌ Larger numeric values (6 digits for later modules) — acceptable for enum usage

---

### ADR-034: Notifications Module — Template-Based Dispatch with Email + In-App Channels (2026-04-30)

**Context:**
- MarketNest needs a flexible notifications system for both real-time and scheduled messages.
- Notifications must support multiple channels: Email, SMS, In-App, etc.
- Admin users should manage templates and triggers without code changes.

**Decision:**
- New `MarketNest.Notifications` module with `notifications` PostgreSQL schema
- **Template-based system**: notification templates stored in DB, editable via Admin UI
- **Channel support**: Email and In-App notifications implemented in Phase 1; SMS and others can be added later
- **Triggers**: notifications can be sent immediately or scheduled for later
- **Batching**: support for batch sending of notifications to reduce load

**Alternatives Considered:**
- Separate microservice for notifications → Rejected: unnecessary complexity in Phase 1
- Polling-based approach → Rejected: less efficient and more complex than event-driven

**Consequences:**
- ✅ Flexible and extensible notifications system
- ✅ Admin users can manage without code changes
- ✅ Phase 3 ready: can be extracted to a microservice with minimal changes

---

### ADR-035: SharedViewPaths — Centralized Razor Partial Path Constants (2026-04-30)

**Context:**
- All shared Razor partial paths (e.g., `~/Pages/Shared/Forms/_TextField.cshtml`) were repeated as magic strings wherever a shared component was used.
- Violates ADR-005 (No Magic Strings): any path rename would require a grep-and-replace across all views.
- `SharedViewPaths` already existed with a single entry (`LoadingSpinner`) — pattern was established but not fully applied.

**Decision:**
- Expand `SharedViewPaths.cs` (`MarketNest.Web.Infrastructure`) to contain all shared component paths as `public const string` fields grouped by category (Display, Form).
- All Razor views must reference `SharedViewPaths.*` constants instead of hardcoded `~/Pages/Shared/…` strings when using `<partial name="…">` or `Html.PartialAsync(…)`.
- Grouped sub-prefixes not needed (class is small enough to remain flat).

**Alternatives Considered:**
- Tag Helpers per component → More abstraction, but higher ceremony for simple partials.
- Keep magic strings in views → Rejected: violates ADR-005 and breaks refactoring safety.

**Consequences:**
- ✅ Single place to update if a partial moves or is renamed
- ✅ Compiler catches misspelled paths (C# const vs string literal in .cshtml)
- ✅ Consistent with existing `AppRoutes` / `AppConstants` / `FieldLimits` centralization pattern
- ❌ Developers must add a constant to `SharedViewPaths` before using a new shared partial

---

### ADR-036: Rich Text Editor — Trix with Server-Side HTML Sanitization (2026-04-30)

**Context:**
- `ProductDescription` and `StorefrontDescription` fields use `FieldLimits.MultilineDocument` (max 20,000 chars) and need rich formatting (bold, italic, lists, headings, images).
- Plain `<textarea>` cannot handle rich text. The project uses HTMX + Alpine.js without a bundler — no React/Vue editor available.
- Need inline image upload support (drag/paste into editor → upload → display inline).

**Decision:**
- Use **Trix Editor** (MIT, by Basecamp) — vendored in `wwwroot/lib/trix/` (v2.1.12). Bundle ~50KB gzip.
- Shared Razor partial: `_RichTextEditor.cshtml` in `Pages/Shared/Forms/`, referenced via `SharedViewPaths.RichTextEditor`.
- Alpine.js component: `richEditor.js` with config from `constants.js` (`RichEditorConfig`).
- Output: HTML string stored directly in DB (no Delta/Markdown conversion layer).
- **Server-side sanitization mandatory**: `IHtmlSanitizerService` (interface in `Base.Common/Contracts/`) implemented by `TrixHtmlSanitizerService` (Web host) using `HtmlSanitizer` NuGet package. Whitelists only Trix-generated tags/attrs.
- Image uploads via dedicated endpoint (`/api/v1/uploads/rich-editor-image`), size limit 2MB, types: JPEG/PNG/WebP/GIF.
- Constants: `FieldLimits.RichEditorImage` (server-side limits), `RichEditorConfig` (JS constants).
- Rendering: `@Html.Raw(model.Description)` safe because content is sanitized at write time. Styled with `.rich-content` CSS class.

**Alternatives Considered:**
- Quill → Larger bundle (~100KB), no native image upload, Delta format adds conversion complexity.
- TinyMCE → 200KB+, overkill for scope, plugin ecosystem overhead.
- Markdown with preview → Worse UX for non-tech sellers; extra Markdown→HTML conversion at render.
- Contenteditable DIY → High risk, incompatible with accessibility, XSS-prone.

**Consequences:**
- ✅ Lightweight, MIT, zero bundler dependency
- ✅ Native image drag/paste upload
- ✅ HTML string output — no conversion layer, direct DB storage and render
- ✅ XSS prevention via server-side whitelist sanitization (never trust client HTML)
- ❌ No video support (acceptable — out of scope per business rules)
- ❌ Trix toolbar is opinionated — limited heading levels (h1 only natively)
- ❌ Vendored library requires manual version updates

---

## ADR-037 — Excel Import/Export — ClosedXML + IExcelService + IAntivirusScanner

**Date**: 2026-04-30
**Status**: Accepted

**Context:**
- Phase 1 requires seller bulk product/variant import (upload .xlsx → validate → execute) and admin export (orders, payouts, users).
- EPPlus has commercial license restrictions. OpenXml SDK is low-level and verbose.
- Need a contract-first design so module code never references the Excel library directly.
- File uploads require virus scanning before processing to prevent malicious files.

**Decision:**
- **ClosedXML 0.104.1** (MIT) as the primary Excel library for both import and export.
- **MiniExcel** planned for Phase 2 as a streaming fallback for large exports (>10k rows).
- **`IExcelService`** contract lives in `MarketNest.Base.Common/Excel/` — used by all module handlers.
- **`ClosedXmlExcelService`** implementation lives in `MarketNest.Web/Infrastructure/Excel/`.
- **`IAntivirusScanner`** contract in `Base.Common/Security/` — Phase 1: `NoOpAntivirusScanner` (always clean). Phase 2/3: ClamAV via socket (nClam or clamd binding).
- **Contracts** (`ExcelTemplate<T>`, `ExcelImportResult<T>`, `ExcelExportOptions<T>`) in `Base.Common/Excel/` — enum `ExcelColumnFormat.DecimalNumber` (not `Decimal` — avoids CA1720).
- **`System.IO.Packaging` CVE fix**: ClosedXML 0.104.1 transitively pulls in `System.IO.Packaging 8.0.0` (CVE-2024-43483, CVE-2024-43484 — DoS, high). Pinned to 10.0.0 in `Directory.Packages.props` and referenced explicitly in `MarketNest.Web.csproj`.
- **Validation layers** (4 layers): file extension + magic bytes → antivirus → header validation → row-level parse → domain rules.
- **`VariantImportTemplate`**: Phase 1 import targets `ProductVariant` entities (the only Catalog entity in DbContext). When `Product` aggregate is added, a `ProductImportTemplate` will be added.
- **Phase 1 imports**: Synchronous (parse + commit in one HTTP request, max 1,000 rows).
- **Phase 2 imports**: Redis import session (30 min TTL) + background jobs for large files.

**Consequences:**
- ✅ Module Application layer never references ClosedXML — swap is a single DI binding change
- ✅ Column definitions use `Func<string, TRow, Result<Unit, string>>` setters — type-safe, testable
- ✅ Magic-bytes check prevents extension spoofing (e.g., renamed .exe to .xlsx)
- ✅ Antivirus hook is in place from Phase 1 — easily upgraded to real ClamAV
- ✅ `System.IO.Packaging` CVE patched via explicit version pin
- ❌ ClosedXML has no native async API — offloaded to `Task.Run` (acceptable for <10k rows)
- ❌ FindBySkuAsync is a no-op stub in Phase 1 — update handler creates all rows; Phase 2 adds the real query

---

### ADR-038: I18N Service — II18NService Wrapper + I18NKeys Constants (2026-04-30)

**Context:**
- Auth pages and the Home page had Vietnamese strings hardcoded directly in `.cshtml` views — not using the resource system.
- The existing `IStringLocalizer<SharedResource>` pattern was verbose (`SharedLocalizer["Key"]`) and offered no compile-time key safety (string keys typo'd silently fell back to the raw key text).
- Need a clean wrapper that provides strongly-typed key constants and a simple indexer syntax usable from Razor views.

**Decision:**
- New `II18NService` interface + `I18NService` implementation in `MarketNest.Web/Infrastructure/Localization/`.
- `I18NKeys` static class (nested: `Page`, `Label`, `Button`, `Text`, `Link`, `Nav`, `Auth`) provides all resource key constants — no inline string keys in views.
- Registered as `Scoped`; injected into Razor via `_ViewImports.cshtml` as `@inject II18NService I18N`.
- View syntax: `@I18N[I18NKeys.Category.Key]` (indexer) or `@I18N.Get(key, args)` (parametrized).
- `I18NService` uses `IAppLogger<T>` with `[LoggerMessage]` delegates for missing key warnings (EventIds `10800`, `10801`).
- Resource files at `src/MarketNest.Web/Resources/SharedResource.{culture}.resx` — expanded with 50+ new keys for both `en` and `vi`.

**Alternatives Considered:**
- Keep `IStringLocalizer<SharedResource>` directly → Rejected: no compile-time key safety; verbose syntax; no project-wide key inventory.
- Source generator for tightly-typed resources → Considered; overkill for Phase 1.

**Consequences:**
- ✅ Compile-time key safety — mistyped keys cause a build error (C# const, not a string literal)
- ✅ Consistent `@I18N[I18NKeys.X.Y]` syntax replaces scattered `@SharedLocalizer["..."]` calls
- ✅ Coexists with existing `IStringLocalizer` — both resolve the same `.resx` files; migration is gradual
- ❌ Developers must add new keys to `I18NKeys.cs` AND `.resx` files before using them

---

### ADR-039: Nullable Management — Business Decision Model with `#pragma` on EF Constructors (2026-04-30)

**Context:**
- `Directory.Build.props` has `<Nullable>enable</Nullable>` + `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` — any nullable warning breaks CI.
- Developers (and AI agents) used `= default!`, `= null!`, and `= string.Empty` sentinels to silence CS8618 warnings rather than thinking about whether a property is truly optional.
- No canonical document stated the *project rule* for choosing between nullable and non-nullable: the choice was made per-taste rather than per-domain logic.

**Decision:**
- Nullable is a **business decision**, not an implementation detail.
- Every `?` must have a domain-reason comment explaining why absent is a valid domain state.
- **Entity rules**: Non-nullable = required invariant. Nullable = "not yet happened" (ShippedAt) or "optional FK" (CouponId). Collections: always initialized with `[]`, never nullable.
- **EF Core private constructors**: use `#pragma warning disable CS8618` on the constructor body only — never on the class, never `= default!` on fields.
- **Value Objects**: NEVER nullable properties — VOs represent complete data.
- **DTOs / Commands / Queries**: Required → use `required` keyword (non-nullable). Optional → nullable `?`. Never `string.Empty` sentinels.
- **Canonical reference**: `docs/nullable-management.md` (rules + quick-ref table + anti-patterns + checklist).

**Alternatives Considered:**
- `= default!` / `= null!` everywhere → Rejected: silences warnings without communicating intent; hides missing domain modeling.
- `#pragma` on entire class → Rejected: suppresses warnings for all fields, including ones that should actually be non-nullable.
- `#nullable disable` in EF constructors → Rejected: disables more than necessary; `#pragma disable CS8618` on the constructor alone is surgical.

**Consequences:**
- ✅ Every nullable property communicates domain intent — reviewers can verify correctness
- ✅ No sentinel values (`string.Empty`, `default!`) leaking into domain entities
- ✅ Checklist in `nullable-management.md` speeds up code review
- ✅ Consistent pattern for EF Core constructors across all modules
- ❌ Requires discipline: developers must add domain comments, not just add `?` to stop the compiler

---

### ADR-040 — Period-Scoped PostgreSQL Sequences for Running Numbers

**Date**: 2026-04-30
**Status**: Accepted

**Context:**
Running number generation (e.g., `ORD202604-00001`) needs to be deadlock-free, race-condition-safe, and support reset by month/year under high concurrent traffic.

**Options considered:**
1. `MAX(id)+1` — not concurrent-safe, can produce duplicates
2. `ALTER SEQUENCE RESTART` via cron — has a race window between ALTER and NEXTVAL
3. `SELECT FOR UPDATE` counter table — serializes all writes, bottleneck
4. **Period-scoped sequence names** — new PG sequence per period, no reset needed

**Decision:**
Use period-scoped PostgreSQL sequences. Each period (month/year) gets its own sequence object (e.g., `orders.seq_ord_202604`). Sequences auto-provision on first use via `CREATE SEQUENCE IF NOT EXISTS` (PG catalog lock serializes concurrent DDL).

**Consequences:**
- ✅ Zero race condition — no `ALTER SEQUENCE RESTART` needed
- ✅ Deadlock-free — NEXTVAL is always non-blocking
- ✅ Concurrent-safe — PG guarantees unique values from SEQUENCE
- ✅ Auto-provision — first request in new period creates sequence (cached in-process)
- ✅ Old sequences cleaned up by monthly background job
- ❌ Accumulates ~12 sequences/year per monthly descriptor (negligible DB catalog overhead)

**Implementation:**
- Contracts: `MarketNest.Base.Common/Sequences/` (`ISequenceService`, `SequenceDescriptor`, `SequenceResetPeriod`)
- Infrastructure: `MarketNest.Web/Infrastructure/Sequences/PostgresSequenceService.cs` (Singleton)
- Module descriptors: `OrderSequences`, `PaymentSequences`, `CatalogSequences`
- Cleanup: `CleanupStaleSequencesJob` (`common.cleanup-stale-sequences`, monthly)
- Full spec: `docs/sequence-service.md`

### ADR-041 — Optimistic Concurrency Control via IConcurrencyAware + UpdateToken

**Date**: 2026-04-30
**Status**: Accepted

**Context:**
Multiple users may concurrently edit the same entity (product, storefront, order). Without concurrency control, the "last write wins" — silently overwriting another user's changes. Bulk update operations compound this risk.

**Decision:**
Implement opt-in optimistic concurrency using a `Guid UpdateToken` field:

1. **Interface**: `IConcurrencyAware` (`Base.Domain`) — entities opt-in by implementing `UpdateToken { get; }` and `RotateUpdateToken()`.
2. **Interceptor**: `UpdateTokenInterceptor` (`Base.Infrastructure`) — auto-rotates token on every `Added`/`Modified` entity during `SaveChanges`.
3. **EF Core config**: `ApplyConcurrencyTokenConventions()` in `DddModelBuilderExtensions` — configures `IsConcurrencyToken()` so EF Core adds `WHERE UpdateToken = @original` to UPDATE/DELETE.
4. **Handler pre-check**: `ConcurrencyGuard.CheckToken(entity, command.UpdateToken)` — returns `Error.ConcurrencyConflict(...)` before mutation if stale.
5. **Safety net**: `UnitOfWork.CommitAsync` catches `DbUpdateConcurrencyException` → `ConcurrencyConflictException` → transaction filters return HTTP 409 Conflict.
6. **Bulk strategy**: Fail-all with stale-item reporting — `ConcurrencyGuard.CheckTokens(...)` validates all tokens before any mutation and returns all stale IDs.

**Alternatives considered:**
- `xmin`/`rowversion` PostgreSQL system column — less portable, implicit, can't be projected to DTOs easily.
- `[ConcurrencyCheck]` on `ModifiedAt` field — coarser, same-second edits may falsely succeed.
- MediatR pipeline behavior for automatic validation — rejected because check requires loading the entity first (which the handler already does), and bulk commands have varied shapes.

**Consequences:**
- ✅ Two-layer protection: explicit pre-check in handler (clear error message) + EF Core safety net (catches race conditions)
- ✅ Opt-in per entity — no performance overhead on entities that don't need it
- ✅ Bulk operations report exactly which records are stale
- ✅ Zero changes required in read-side DbContexts
- ❌ Requires all read DTOs to include `UpdateToken` for updatable entities
- ❌ Each module must call `ApplyConcurrencyTokenConventions()` in `OnModelCreating`

**Implementation:**
- Interface: `src/Base/MarketNest.Base.Domain/IConcurrencyAware.cs`
- Interceptor: `src/Base/MarketNest.Base.Infrastructure/Persistence/Persistence/UpdateTokenInterceptor.cs`
- Model config: `ApplyConcurrencyTokenConventions()` in `DddModelBuilderExtensions.cs`
- Guard: `src/Base/MarketNest.Base.Infrastructure/Persistence/Persistence/ConcurrencyGuard.cs`
- Exception: `src/Base/MarketNest.Base.Infrastructure/Persistence/Persistence/ConcurrencyConflictException.cs`
- Error factories: `Error.ConcurrencyConflict(entity, id)`, `Error.BulkConcurrencyConflict(entity, staleIds)`
- Validator: `ValidatorExtensions.MustBeValidUpdateToken()` in `Base.Common`
- Filter handling: `RazorPageTransactionFilter` + `TransactionActionFilter` catch `ConcurrencyConflictException` → 409

---

### ADR-042: MN019/MN020 — Handler Entity Return & QueryHandler Select-Projection Analyzer Rules (2026-04-30)

**Context:**
- Handlers that return `Entity<T>` or `AggregateRoot` subtypes leak domain state through the CQRS boundary, break SRP (the caller now has full domain object access), and couple the read contract to aggregate structure.
- LINQ queries without a `.Select()` projection load every column from the database even if only two columns are actually needed, causing unnecessary I/O at scale. This was already a code-rule guideline but had no build-time enforcement.

**Decision:**
- **MN019** (`Warning`) — `HandlerEntityReturnAnalyzer`: fires on any class implementing `ICommandHandler<TC, TResult>` or `IQueryHandler<TQ, TResult>` where `TResult` (unwrapped through `Task<T>`, `Result<T,E>`, `IEnumerable<T>`, `IReadOnlyList<T>`, etc.) is a subtype of `Entity<T>` or `AggregateRoot`. Fix: return a DTO or result record with only the fields the use-case needs.
- **MN020** (`Warning`) — `HandlerQueryProjectionAnalyzer`: fires on terminal LINQ operators (`ToListAsync`, `FirstOrDefaultAsync`, `SingleOrDefaultAsync`, etc.) inside `IQueryHandler<,>` or `BaseQuery<,,>` subclasses when no `.Select()` or `.SelectMany()` appears in the same fluent chain. CommandHandlers are intentionally excluded — they legitimately need the full aggregate state to enforce invariants. Fix: add `.Select(e => new Dto { … })` before the terminal call, or suppress with `#pragma warning disable MN020` + reason.

**Trade-offs:**
- Severity `Warning` (not `Error`) because both rules have legitimate exemption cases. With `TreatWarningsAsErrors=true` in `Directory.Build.props` they still fail CI.
- MN020 uses pure syntax chain-walking (no semantic model needed) — fast, but cannot follow queries split across multiple local variables. Complex multi-variable EF queries should be suppressed per-site.
- MN019 recursive type-unwrapping handles up to 3 levels of nesting; beyond that the false-positive risk is low in practice.

**Consequences:**
- ✅ Build-time enforcement of the "handlers return DTOs only" rule — replaces a human code-review checklist item
- ✅ Prevents accidental full-entity SELECT in every query — measurable N+1 and column-bloat reduction
- ❌ Teams must add `#pragma warning disable MN020` for CommandHandler's legitimate entity-load patterns
- ❌ MN020 cannot track queries split across multiple `var query = …` statements

**Implementation:**
- `src/MarketNest.Analyzers/Analyzers/Architecture/HandlerEntityReturnAnalyzer.cs`
- `src/MarketNest.Analyzers/Analyzers/Architecture/HandlerQueryProjectionAnalyzer.cs`
- Tests: `tests/MarketNest.Analyzers.Tests/Architecture/HandlerEntityReturnAnalyzerTests.cs` (9 tests)
- Tests: `tests/MarketNest.Analyzers.Tests/Architecture/HandlerQueryProjectionAnalyzerTests.cs` (7 tests)
- Docs: `docs/analyzers.md` — table + rule reference sections updated

---

### ADR-043: Announcement Feature Foundation — Admin-Managed Site-Wide Announcements (2026-05-01)

**Status**: Implemented ✅ (Phase 1 Foundation)

**Context:**
- Admin needs to broadcast promotional and operational announcements (Black Friday, voucher launches, maintenance windows) across the entire marketplace.
- Announcements must appear in two prominent locations: the announcement banner below the navbar (every page) and optionally in the hero section of the homepage.
- Phase 1 needs a solid foundation: entity model, CQRS stack, scheduling, and display — without building the full admin management UI yet.

**Decision:**
- `Announcement` entity lives in `MarketNest.Admin` module (`admin` schema) — Admin is the correct owner for platform-wide content.
- **Entity fields**: `Title`, `Message`, `AnnouncementType` (Info/Promotion/Warning/Urgent), `LinkUrl?`, `LinkText?`, `StartDateUtc`, `EndDateUtc`, `IsPublished`, `IsDismissible`, `SortOrder`.
- **Domain methods**: `Publish()`, `Unpublish()`, `Update()`, `IsActive(DateTimeOffset utcNow)`.
- **CQRS**: 4 write commands (`Create/Update/Delete/PublishAnnouncement`) + 2 read queries (`GetAnnouncementsPaged` for admin list, `GetActiveAnnouncements` for public display).
- `IQuery<TResult>` pattern — query handlers return `TResult` directly (no `Result<T,E>` wrapper) per project convention.
- **Display strategy**: `_AnnouncementBanner.cshtml` Razor partial loaded via HTMX on every page (`hx-get="/Shared/AnnouncementBanner" hx-trigger="load"`). This avoids blocking the initial page render and adds zero latency to page generation.
- **Banner dismissal**: Alpine.js + `localStorage` keyed by announcement ID (`mn-dismiss-{id}`). Stateless server side — no user preferences table needed in Phase 1.
- **Styling**: Type-based color classes (Promotion → accent, Warning → amber, Urgent → red, Info → blue). Dismiss button hidden when `IsDismissible = false`.
- **Routing**: `/Shared/AnnouncementBanner` Razor Page (with `Layout = null`) serves the partial — discovered by the route whitelist.
- **LogEventId block**: 102100–102159 (Announcement handlers within Admin 102xxx Application range).

**Alternatives Considered:**
- Notifications module → Rejected: Notifications is for user-targeted messages (email/inbox). Site-wide banners are platform content — belongs in Admin.
- ViewComponent for banner injection → Considered: ViewComponents are cleaner but add a compile-time dependency; HTMX lazy-load achieves the same result with zero render blocking.
- Server-side dismiss with user preference → Rejected: Phase 1 complexity; localStorage is sufficient for anonymous + logged-in users alike. Phase 2 can add DB-backed dismiss if needed.
- CMS/markdown content → Rejected: raw text + optional URL link is sufficient for Phase 1 use cases (Black Friday, voucher codes).

**Consequences:**
- ✅ Every page can show announcements with zero change to individual page models.
- ✅ Scheduling: announcements auto-appear and auto-expire based on `StartDateUtc`/`EndDateUtc` — no manual unpublish required.
- ✅ `GetActiveAnnouncements` query filters entirely in the DB (indexed on `IsPublished + StartDateUtc + EndDateUtc`) — one small read per page load.
- ✅ Admin CRUD UI (`/admin/announcements` Razor Page) is the logical next step — all backend is ready.
- ✅ Phase 2: add OutputCache (1–2 min TTL) on `GetActiveAnnouncements` to reduce DB reads on high-traffic pages.
- ❌ localStorage dismiss is per-browser, not per-user — logged-in users will see dismissed announcements again on other devices.
- ❌ No hero-section integration yet — `AnnouncementHero` partial is the next UI task.

**Implementation files:**
- `src/MarketNest.Admin/Domain/Modules/Announcement/Entities/Announcement.cs`
- `src/MarketNest.Admin/Domain/Modules/Announcement/Entities/AnnouncementType.cs`
- `src/MarketNest.Admin/Application/Modules/Announcement/` (DTOs, Commands, Queries, Handlers, Validators, Repository interface)
- `src/MarketNest.Admin/Infrastructure/Persistence/Configurations/AnnouncementConfiguration.cs`
- `src/MarketNest.Admin/Infrastructure/Repositories/Modules/Announcement/AnnouncementRepository.cs`
- `src/MarketNest.Admin/Infrastructure/Queries/Modules/Announcement/AnnouncementQuery.cs`
- `src/MarketNest.Web/Pages/Shared/Display/_AnnouncementBanner.cshtml`
- `src/MarketNest.Web/Pages/Shared/AnnouncementBanner.cshtml` + `.cs` (HTMX endpoint page)
- Modified: `AdminDbContext`, `AdminReadDbContext`, `DependencyInjection.cs`, `LogEventId.cs`, `AppRoutes.cs`, `SharedViewPaths.cs`, `_Layout.cshtml`
