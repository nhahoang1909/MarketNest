# Work Log

Quick-reference log of completed and in-progress work. Full details live in GitHub issues/PRs.
Each entry should be **scannable in 30 seconds** — link to PRs for full context.

**Archiving policy**: Move entries older than 3 months to `issues-archive-YYYY.md`.
Keep a reference: _"See `issues-archive-2026.md` for older entries."_

**When this file exceeds ~20 entries**: Add a Table of Contents at the top.

## Format

### YYYY-MM-DD - Brief Description
- **Status**: Completed / In Progress / Blocked
- **Description**: 1-2 line summary
- **PR/Issue**: Link if available
- **Notes**: Any important context

---

## Entries

### 2026-04-30 - feat(base): PgQueryBuilder — safe raw PostgreSQL query generation utility (ADR-032)
- **Status**: Completed
- **Description**: Added `PgQueryBuilder` static utility class to `Base.Infrastructure/Persistence/` for safely generating raw PostgreSQL queries when EF Core is insufficient (complex multi-schema joins, DDL, PostgreSQL-specific features). Key features:
  - Parameterized query building via `FormattableString` interpolation ($1, $2, … positional params)
  - Identifier quoting with double-quote escaping (prevents injection via table/column names)
  - Builders: `Query`, `Select`, `Insert`, `InsertMany`, `Update`, `Delete`, `Upsert`, `InClause`, `NotInClause`, `Combine`, `EscapeLike`
  - `[GeneratedRegex]` source-generated regexes (zero allocation)
  - `PgQuery` sealed record (immutable result type)
  - `ToDebugString` for dev logging (not for execution)
- **Files changed**: `src/Base/MarketNest.Base.Infrastructure/Persistence/PgQueryBuilder.cs` (new)
- **Docs updated**: `docs/backend-infrastructure.md` §1.4, `docs/project_notes/decisions.md` (ADR-032), `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`
- **ADR**: ADR-032
- **Build**: `dotnet build` → 0 warnings, 0 errors ✅

---

### 2026-04-30 - config: Two-connection-string pattern — DefaultConnection + ReadConnection fallback (ADR-031)
- **Status**: Completed
- **Description**: Introduced `ReadConnection` as an optional second connection string. Empty value in Phase 1 — all ReadDbContexts fall back to `DefaultConnection`. Phase 2: set to a PostgreSQL read replica for zero-code-change read scaling. Rejected per-module connection strings (AuditConnection, NotificationConnection) as premature — module extraction at Phase 3 requires far more than a connection string change.
- **Files changed**:
  - `src/MarketNest.Web/appsettings.json` — added `ReadConnection: ""`
  - `src/MarketNest.Admin/Infrastructure/DependencyInjection.cs` — fallback pattern applied
  - `docker-compose.yml` — added `ConnectionStrings__ReadConnection` env var (default empty)
  - `.env.example` — documented `CONNECTION_STRINGS__READCONNECTION=` with Phase 2 note; fixed var name (`DEFAULT` → `DEFAULTCONNECTION`)
- **ADR**: ADR-031 added to `docs/project_notes/decisions.md`
- **Notes**: All other modules should adopt the same fallback pattern in their `DependencyInjection.cs` when implementing their ReadDbContext.

---

### 2026-04-29 - feat(base): Add batch write methods to IBaseRepository + BaseRepository
- **Status**: Completed
- **Description**: Extended `IBaseRepository<TEntity,TKey>` and `BaseRepository<TEntity,TKey,TContext>` with three batch write methods for multi-record operations:
  - `AddRangeAsync(IEnumerable<TEntity>, CancellationToken)` — tracks multiple new entities in one call
  - `UpdateRangeAsync(IEnumerable<TEntity>, CancellationToken)` — marks multiple entities as modified
  - `RemoveRangeAsync(IEnumerable<TEntity>, CancellationToken)` — marks multiple entities for deletion
  - All existing single-entity `Add`/`Update`/`Remove` methods retained (sync, EF Change Tracker only)
  - All writes still flushed via `IUnitOfWork.CommitAsync()` — no `SaveChangesAsync()` calls added
- **Files changed**: `src/Base/MarketNest.Base.Infrastructure/Persistence/Persistence/IBaseRepository.cs`, `src/Base/MarketNest.Base.Infrastructure/Persistence/Persistence/BaseRepository.cs`
- **Docs updated**: `docs/backend-patterns.md` §6, `AGENTS.md`, `CLAUDE.md`, `.github/copilot-instructions.md`
- **Build**: `dotnet build MarketNest.slnx` → 0 warnings, 0 errors ✅

---

### 2026-04-29 - refactor: Unit of Work pattern — filters own transaction lifecycle (ADR-027 update)
- **Status**: Completed
- **Description**: Major refactoring of the Unit of Work pattern. Previously, command handlers called `uow.CommitAsync()` explicitly (error-prone, could silently lose data if forgotten). Now:
  - **HTTP handlers** (Razor Pages + API controllers): filters (`RazorPageTransactionFilter` / `TransactionActionFilter`) own the full transaction lifecycle. Handlers only mutate entities via repositories — no UoW injection needed. Filter automatically calls `BeginTransactionAsync` → next() → `CommitAsync` → `CommitTransactionAsync` → `DispatchPostCommitEventsAsync` → `DisposeAsync`.
  - **Background jobs**: explicitly manage transactions using the new UoW methods. Full try/catch/finally lifecycle ensures atomicity.
  - **IUnitOfWork expanded** (now `IAsyncDisposable`): added `BeginTransactionAsync`, `CommitTransactionAsync`, `RollbackAsync`, `DisposeAsync` methods. Moved transaction object storage from filters into UoW.
  - **UnitOfWork implementation** updated: stores `Dictionary<DbContext, IDbContextTransaction>` internally. No more loop logic in filters.
  - **Filters simplified**: removed transaction management loops, now delegate to `uow.BeginTransactionAsync/CommitTransactionAsync/RollbackAsync`.
  - **All 7 command handlers** (Admin, Promotions, Catalog): removed explicit `uow.CommitAsync()` calls + IUnitOfWork injection. Handlers simplified to pure domain mutation.
  - **Background jobs** (ExpireSalesJob, VoucherExpiryJob): updated to use new explicit transaction management pattern.
  - **Documentation**: Updated `docs/backend-patterns.md` §22 + CLAUDE.md/AGENTS.md/copilot-instructions.md UoW conventions. Updated ADR-027 in decisions.md with full revised decision + consequences.
  - **LogEventId**: Added `UoWTxBegin`, `UoWTxCommitted`, `UoWTxRolledBack` (1074–1076), job error IDs.
- **Build**: `dotnet build` → 0 errors ✅
- **Benefits**: 
  - ✅ Handlers can't forget to commit (filter handles it automatically)
  - ✅ No silent data loss — uncommitted changes are impossible in HTTP handlers
  - ✅ Clear separation: HTTP (auto) vs background jobs (explicit control)
  - ✅ Atomicity guaranteed: all entity changes + pre-commit events in same TX or rollback together
- **ADR**: ADR-027 updated (see decisions.md for full entry)

---

### 2026-04-29 - refactor: Application Constants vs Configuration policy (ADR-030)
- **Status**: Completed
- **Description**: Refactored configuration management to distinguish between immutable business rules and environment-specific settings:
  - **AppConstants.Validation** section (new): moved business rule constants from `appsettings.json` Validation section. Password length (8-128), username length (3-50), file upload limits (5MB images, 10MB documents, max 5 per upload).
  - **appsettings.json** (cleaned): removed Validation section; kept Security section for environment-tunable values (rate limits, lockout duration, token expiry).
  - **Deprecated `ValidationOptions`** class: no longer needed since all validation rules are now in `AppConstants`. Direct access to `AppConstants.Validation.*` replaces `IOptions<ValidationOptions>` binding.
  - **Documentation**: Updated `docs/code-rules.md` §2.6 with clear distinction (with example code), updated CLAUDE.md / AGENTS.md / copilot-instructions.md to include ADR-030 guidance.
  - **ADR**: ADR-030 added to `decisions.md` (see above for full ADR entry).
- **Build**: `dotnet build` → 0 errors ✅
- **Rationale**: Reduces appsettings.json bloat. Improves code readability (`AppConstants.Validation.PasswordMinLength` is clearer than `Configuration["Validation:PasswordMinLength"]`). Forces developers to distinguish between business rules and tuning parameters.

---

### 2026-04-29 - Project docs & guidance updated — ADR-030 + spec tables
- **Status**: Completed
- **Description**: Updated all project guidance and documentation files to reflect ADR-030 (AppConstants vs appsettings):
  - **`docs/project_notes/decisions.md`**: Added ADR-030 to TOC, full ADR entry with context, decision, rationale, consequences.
  - **CLAUDE.md / AGENTS.md / .github/copilot-instructions.md**: Updated "Current status" section to mention ADR-030; updated "Key Conventions" / code rules sections to include the AppConstants vs appsettings distinction with pattern examples.
  - **`docs/code-rules.md`** §2.6: Added subsection explaining the distinction (business rules in AppConstants, environment tuning in appsettings), with code examples, enforcement notes.
- **Docs**: Reference docs (`backend-patterns.md`, `caching-strategy.md`, `frontend-guide.md`) already had correct patterns — no changes needed.
- **Build**: Documentation-only; no code changes.

---

## Entries

### 2026-04-29 - feat(infra): Four-layer caching strategy implementation (ADR-029)
- **Status**: Completed
- **Description**: Implemented the Phase 1 caching foundation:
  - **Layer 1 (Static assets)**: Added `asp-append-version="true"` to all local JS script tags across 3 layouts (`_Layout`, `_LayoutSeller`, `_LayoutAdmin`). Custom `StaticFileOptions` with `Cache-Control: immutable` for fingerprinted files, `max-age=86400` for media, `no-cache` for others.
  - **Layer 1b (HTMX)**: New `HtmxNoCacheMiddleware` — forces `no-store` on all `HX-Request` responses.
  - **Layer 2 (OutputCache)**: Three named policies (`AnonymousPublic` 60s, `Storefront` 5m, `ProductDetail` 2m) for anonymous-only Razor Pages. New `CachePolicies` constants class.
  - **Layer 3 (Redis)**: Expanded `CacheKeys` with `Catalog`, `Cart`, `Payments`, `Identity`, `Admin` nested classes and new TTL presets (`VeryShort` 30s, `QuickExpiry` 1m, `VeryLong` 6h).
  - **Redis safety**: Upgraded `RemoveByPrefixAsync` from `KEYS` to `SCAN`-based cursor iteration with batched deletion.
  - **Cross-module decision**: Service contracts via interfaces (in-process DI) — not gRPC or BFF. Deferred to Phase 3.
  - **Docs**: New `docs/caching-strategy.md`, updated `backend-infrastructure.md` CacheKeys section, updated spec docs tables in `AGENTS.md`/`CLAUDE.md`/`copilot-instructions.md`.
- **ADR**: ADR-029 (see decisions.md)
- **Build**: `dotnet build MarketNest.slnx` → 0 warnings, 0 errors ✅

### 2026-04-29 - feat(core): IRuntimeContext — unified ambient request/job context
- **Status**: Completed
- **Description**: Implemented `IRuntimeContext` + `ICurrentUser` as the single injection point for user identity, correlation ID, request metadata, and timing:
  - **`UnauthorizedException`** (new, `Base.Common`) — thrown by `ICurrentUser.RequireId()` when user is anonymous.
  - **`ICurrentUser`** (new, `Base.Common`) — `Id?`, `Name?`, `Email?`, `Role?`, `IsAuthenticated`, `RequireId()`, `IdOrNull`.
  - **`IRuntimeContext`** (new, `Base.Common`) — `CorrelationId`, `RequestId`, `CurrentUser`, `Execution`, `StartedAt`, `ElapsedMs`, HTTP metadata.
  - **`RuntimeExecutionContext`** enum (new, `Base.Common`) — `HttpRequest | BackgroundJob | Test`.
  - **`CurrentUser`** (new, `Web.Infrastructure/Runtime/`) — ClaimsPrincipal-backed; has `IsAdmin/IsSeller/IsBuyer` helpers.
  - **`AnonymousUser`** / **`SystemJobUser`** — internal singletons for anonymous and admin-triggered job users.
  - **`HttpRuntimeContext`** (new, Scoped) — mutable backing object for HTTP requests, populated by middleware.
  - **`BackgroundJobRuntimeContext`** (new) — immutable; `ForSystemJob(jobKey)` and `ForAdminJob(jobKey, adminId)` static factories.
  - **`RuntimeContextMiddleware`** (new) — enriches Serilog LogContext (CorrelationId, UserId, UserRole), tags OTel Activity, echoes `X-Correlation-ID` header. Registered after `UseAuthorization()`.
  - **`TestRuntimeContext`** + `FakeCurrentUser` (new, `UnitTests/Helpers/`) — `AsAnonymous()`, `AsBuyer()`, `AsSeller()`, `AsAdmin()` builders.
  - **`LogEventId`** — added `RuntimeContextRequestStart` (1094), `RuntimeContextRequestEnd` (1095).
  - **`Program.cs`** — DI registration (`HttpRuntimeContext` Scoped, `IRuntimeContext` → 0 warnings, 0 errors ✅

### 2026-04-29 - feat(core): Unit of Work + [Transaction] attribute + domain event lifecycle split
- **Status**: Completed
- **Description**: Implemented the full UoW + transaction-attribute infrastructure (ADR-027):
  - **`IHasDomainEvents`** (new, `Base.Domain`) — non-generic interface on `Entity<TKey>`; allows `UnitOfWork` ChangeTracker scan without generic key constraint.
  - **`IPreCommitDomainEvent`** (new, `Base.Domain`) — marker for pre-commit (executing) domain events that run INSIDE the DB transaction before SaveChanges. All other domain events remain post-commit (executed after TX commit).
  - **`IUnitOfWork`** (new, `Base.Infrastructure`) — single persist entry-point. `CommitAsync` = pre-commit events + `SaveChangesAsync`. `DispatchPostCommitEventsAsync` = post-TX event dispatch with safe failure handling.
  - **`[Transaction]` / `[NoTransaction]`** attributes (new, `Base.Common`) — control transaction wrapping on Razor Pages and API controllers. Supports `IsolationLevel` + `TimeoutSeconds`.
  - **`UnitOfWork`** (new, `MarketNest.Web.Infrastructure/Persistence/`) — scans all `IModuleDbContext` instances, dispatches events, calls `SaveChangesAsync` on each.
  - **`RazorPageTransactionFilter`** (new, `MarketNest.Web.Infrastructure/Filters/`) — globally registered, auto-wraps all OnPost* handlers. OnGet* always bypassed.
  - **`TransactionActionFilter`** (new, `MarketNest.Web.Infrastructure/Filters/`) — globally registered, activates only when `[Transaction]` attribute present on controller/action.
  - **`ReadApiV1ControllerBase` / `WriteApiV1ControllerBase`** (new, `Base.Api`) — write controllers carry `[Transaction]` class-level attribute automatically.
  - **`LogEventId`** — added 10 new event IDs (1071–1093) for UoW, RazorPageTx, ActionTx.
  - Updated `TestReadController` / `TestWriteController` to use the new split base classes.
  - **Program.cs** — registered `IUnitOfWork`, `RazorPageTransactionFilter`, `TransactionActionFilter` as Scoped; added global filters via `Configure<MvcOptions>`.
- **ADR**: ADR-027 (see decisions.md)
- **Build**: `dotnet build` → 0 errors, 0 warnings ✅

### 2026-04-29 - SLA foundation: doc + constants + PerformanceBehavior + FinancialReconciliationJob
- **Status**: Completed
- **Description**: Reviewed external SLA requirements (`marketnest-docs/business-logic/sla-requirement.md`) against existing domain invariants and implemented Phase 1 foundation:
  - **`docs/sla-requirements.md`** — New canonical SLA document (4 dimensions: Availability, Performance, Business Correctness, Data Integrity). Cross-references domain invariants (I1 oversell, §10.2 formula, P2 voucher constraint), alerts matrix, phased implementation plan.
  - **`Base.Common/SlaConstants.cs`** — Typed constants for all SLA thresholds: `Availability`, `Performance`, `Business`, `Integrity`, `Throughput` nested classes. No magic numbers.
  - **`Auditing/Infrastructure/PerformanceBehavior.cs`** — MediatR pipeline behavior. Logs `Warning` at 1000 ms (`SlowRequestMs`), `Warning` (SLA breach risk) at 3000 ms (`CriticalRequestMs`). Registered as outermost behavior in `AddAuditingModule()`.
  - **`Payments/Application/Timer/FinancialReconciliation/FinancialReconciliationJob.cs`** — Nightly stub at 02:00 UTC. Checks BuyerTotal vs ChargedAmount, orphaned payments, negative payouts. Full logic deferred until Order + Payment aggregates complete; all log delegates are declared and ready.
  - **`Payments.csproj`** + **`GlobalUsings.cs`** — Added `Base.Utility` reference (needed for `IBackgroundJob`, `JobDescriptor`).
  - **`LogEventId.cs`** — Added `PerfBehaviorSlowRequest` (11030), `PerfBehaviorCriticalRequest` (11031), `PaymentsReconciliationJob*` (6100–6104).
- **ADR**: ADR-026 (see decisions.md)
- **Build**: `dotnet build MarketNest.slnx` → 0 errors ✅

### 2026-04-29 - chore(base): promote BaseQuery / BaseRepository to Base.Infrastructure + extract module DI
- **Status**: In Progress (staged, not yet committed)
- **Description**: Canonical `BaseQuery<TEntity,TKey,TContext>` and `BaseRepository<TEntity,TKey,TContext>` abstract classes promoted from Admin-only to `Base.Infrastructure` (namespace `MarketNest.Base.Infrastructure`). `IBaseRepository<TEntity,TKey>` interface also moved to `Base.Infrastructure`. Each module now has a 2-line thin wrapper (`BaseQuery<TEntity,TKey>(ModuleReadDbContext)` and `BaseRepository<TEntity,TKey>(ModuleDbContext)`) inheriting from the canonical base. This standardises the query/repository pattern across all modules and eliminates duplicate implementations. Also extracted proper `AddAuditingModule()` and `AddPromotionsModule()` DI extension methods into `DependencyInjection.cs` files for those modules. Various modules (Orders, Payments, Catalog) updated their `DependencyInjection.cs` and `.csproj` refs accordingly.
- **ADR**: ADR-025 (see decisions.md)
- **Notes**: `IBaseQuery<TEntity,TKey>` (in `Base.Common`) was already defined; `BaseQuery<,,>` is now its canonical `Base.Infrastructure` implementation. Thin module wrappers follow the same 2-line pattern as Admin module.

---

### 2026-04-29 - Project docs update: Sale Price business logic + backend patterns sync
- **Status**: Completed
- **Description**: Synced all project documents to reflect the Catalog Sale Price feature (ADR-024) and fix remaining gaps:
  - **`domain-and-business-rules.md`** (v0.4): Updated §3.2 `ProductVariant` aggregate to match actual Phase 1 implementation (inline `SalePrice/SaleStart/SaleEnd` fields, `StockQuantity` simplified inventory, computed helpers `EffectivePrice()`/`IsSaleActive()`/`DisplayOriginalPrice()`, deferred `Attributes`/`InventoryItem` noted for Phase 2). Added §5.4 Catalog Sale Price business rules (invariants S1–S5, background job, API endpoints, checkout integration contract). Updated §6 domain events (added `VariantSalePriceSetEvent`, `VariantSalePriceRemovedEvent`). Updated §7 invariants (added S1–S5 Sale Price section, updated #1 to `StockQuantity ≥ 0`). Expanded Auto-Actions into a platform-wide "Module Background Jobs" table listing all 8 registered jobs.
  - **`backend-patterns.md`**: Updated §16 Planned Jobs table — added `ExpireSalesJob` (Catalog) and `VoucherExpiryJob` (Promotions), added Module column. Fixed Vietnamese section headings (`Cấu trúc dữ liệu`, `Danh sách Job dự kiến`, `Lộ trình phát triển`) → English.
  - **`CLAUDE.md`** + **`AGENTS.md`**: Updated "Current status" to list implemented modules (ADR-024, Promotions, Auditing, Admin config, Analyzers). Added rules for sale price (`EffectivePrice()` mandate) and background jobs (`IBackgroundJob` contract).
- **Notes**: No code changes — documentation-only update.

---

### 2026-04-29 - Catalog: Sale Price domain implemented (ProductVariant)
- **Status**: Completed
- **Description**: Implemented the full Sale Price feature for `ProductVariant` based on `sale-price-domain-plan.md`. Key deliverables:
  - **Domain**: `ProductVariant` entity with `SalePrice`, `SaleStart`, `SaleEnd` fields + `EffectivePrice()`, `IsSaleActive()`, `DisplayOriginalPrice()` computed helpers + `SetSalePrice()` / `RemoveSalePrice()` domain methods. `VariantSalePriceSetEvent` and `VariantSalePriceRemovedEvent` domain events. `CatalogConstants.Sale` (max duration 90d, job schedule).
  - **Application**: `SetSalePriceCommand` + handler, `RemoveSalePriceCommand` + handler, `SetSalePriceCommandValidator` (FluentValidation), `IVariantRepository` (with `GetExpiredSalesAsync`, `GetByProductAsync`), `ExpireSalesJob` (5-min timer background job).
  - **Infrastructure**: `CatalogDbContext` + `BaseRepository<T,TKey>`, `ProductVariantConfiguration` (EF snake_case, Money conversions, partial index `idx_variants_active_sale`), `VariantRepository`, `VariantSaleSellerController` (`PATCH/DELETE api/v1/seller/products/{id}/variants/{id}/sale`), `VariantSaleAdminController` (`DELETE api/v1/admin/catalog/variants/{id}/sale`).
  - **Migration**: `AddVariantSalePrice` — creates `catalog.variants` table with all fields + `chk_sale_price_positive` and `chk_sale_dates_consistent` CHECK constraints (invariant S5) + partial index.
  - **Shared**: `LogEventId` Catalog application events (3100–3212). `AppRoutes.Api.CatalogV1Prefix` + whitelist entry. `MarketNest.Catalog.csproj` updated (Npgsql EF, Base.Utility, AspNetCore FrameworkReference). `Program.cs` wired `CatalogDbContext`, `IVariantRepository`, `ExpireSalesJob`.
- **Build**: `dotnet build MarketNest.slnx` → 0 warnings, 0 errors ✅
- **Notes**: Cart/Checkout integration (`EffectivePrice` in checkout path, price drift separation) deferred until Cart/Orders modules implement their domain. Authorization (seller owns variant check) is a Phase 1 TODO — currently command accepts `RequestingUserId` but does not enforce ownership.

---



### 2026-04-29 - AGENTS.md: subagent delegation + AI convention sourcing guidelines added
- **Status**: Completed
- **Description**: Added two new agent behavior sections to `AGENTS.md` (uncommitted working change):
  - **"Using specialized subagents"**: agents must delegate complex multi-step tasks to the `Plan` subagent via `run_subagent` tool before writing code
  - **"Source existing AI conventions"**: agents must glob-search for `copilot-instructions.md`, `AGENTS.md`, `CLAUDE.md`, cursor/windsurf rules, and `README.md` before modifying code
- **Notes**: These sections are referenced in the `.github/copilot-instructions.md` attachment. No ADR needed — behavioral guidance, not an architectural decision.

---

### 2026-04-28 - chore(devops): skip Qodana code quality CI workflow
- **Status**: Completed
- **Description**: Temporarily disabled Qodana static analysis CI workflow (`.github/workflows/qodana_code_quality.yml` + `qodana.yaml`). Previously CI ran Qodana on every push; skipped to unblock development until `.NET 10` support is stable in Qodana.
- **Notes**: Re-enable when Qodana releases full .NET 10 support. All 17 Roslyn `MN001–MN017` rules continue to enforce quality locally via `Directory.Build.targets`.

---

### 2026-04-28 - Admin Config pages — sub-sidebar + reference data DataTable pages
- **Status**: Completed
- **Description**: Implemented admin system config pages based on the design prototype (`admin-config.jsx`). Key deliverables:
  - **`_ConfigSubSidebar.cshtml`** shared navigation component in `Pages/Shared/Navigation/` — grouped sidebar (Reference data: Countries, Genders, Telephone codes, Nationalities, Product categories; Business: Commission rates) with active-state highlighting matching the prototype's ConfigSubSidebar pattern
  - **Config/Index.cshtml** redesigned as a config hub page: quick-link cards for all config categories, replaced old Vietnamese hard-coded settings form
  - **5 new DataTable pages**: Country, Gender, PhoneCode, Nationality, ProductCategory — each with header (eyebrow, title, subtitle, active count badge), search bar (disabled, placeholder for Phase 3), data table matching prototype columns, `_StatusBadge` partial reuse, alternating row stripes, empty state fallback
  - **Commission.cshtml** redesigned with sub-sidebar layout, slider + payout window form, Phase 1 read-only note
  - **AppRoutes**: Added `ConfigCountry`, `ConfigGender`, `ConfigPhoneCode`, `ConfigProductCategory`, `ConfigNationality`
  - **LogEventId**: Added `AdminConfigCountryStart` (10680), `AdminConfigGenderStart` (10682), `AdminConfigPhoneCodeStart` (10684), `AdminConfigProductCategoryStart` (10686), `AdminConfigNationalityStart` (10688)
  - **`_LayoutAdmin.cshtml`**: Sidebar "Config" link now points to `/admin/config` (hub) instead of `/admin/config/commission`
  - **Page models**: Inject `IReferenceDataReadService` (Redis-cached, active-only via EF query filter). All are `partial` with `[LoggerMessage]` source-generated delegates
  - All text in English (replaced Vietnamese from old Config/Index)
- **Build**: `dotnet build` → 0 warnings, 0 errors ✅
- **Shared components reused**: `_StatusBadge`, `_EmptyState`, `_ConfigSubSidebar` (new), `_LayoutAdmin`
- **Phase 2 TODO**: Client-side search/sort (Alpine.js), admin-scoped query that bypasses `IsActive` query filter to show inactive records, CRUD actions (edit/activate/deactivate buttons)

---

### 2026-04-28 - Project memory documents updated
- **Status**: Completed
- **Description**: Synced all four project memory files with current codebase state. `key_facts.md`: updated Solution Structure (added Promotions, Auditing, Base/* packages, Analyzers), replaced outdated Specification Documents table with current doc filenames, updated Redis Namespaces (added Tier 1/2 cache keys). `decisions.md`: added Table of Contents (19 ADRs), moved ADR-014 to correct chronological position (after ADR-013), added ADR-020 (canonical agent guidelines), removed duplicate ADR-014 block. `bugs.md`: removed orphan placeholder text left between entries.
- **Notes**: ADR-017, ADR-018, ADR-019 reserved/not yet assigned. Next number to use: ADR-023.

---
### 2026-04-28 - MarketNest.Analyzers complete — all 17 Roslyn rules wired to solution
- **Status**: Completed
- **Description**: Implemented `MarketNest.Analyzers` project: 17 diagnostic rules (MN001–MN017) across four categories (Naming, AsyncRules, Logging, Architecture), 5 code fix providers (MN001, MN003, MN006, MN007, MN017), and 73 tests. Wired to all `src/` projects via `src/Directory.Build.targets`. Fixed all violations surfaced during wiring: Promotions Voucher/VoucherUsage DateTime → DateTimeOffset (MN009); AppLogger.cs MN007 suppress; NpgsqlJobExecutionStore.cs MN004 suppress; MarketNest.Web.csproj MN008 suppress (Razor Pages namespace constraint). Added `docs/analyzers.md` as reference and linked from CLAUDE.md.
- **Branch**: `p1-main-nhahoang`
- **Notes**: All 73 analyzer tests pass. Full solution `dotnet build` clean. `MarketNest.Web` MN008 suppressed at project level because Razor Pages PageModel classes use folder-matched namespaces (`@model` directive + `IndexModel` class-name collisions prevent flat `MarketNest.Web.Pages` namespace).

### 2026-04-27 - MarketNest.Promotions module scaffold completed
- **Status**: Completed
- **Description**: Scaffolded the full `MarketNest.Promotions` module (45 files) following existing module patterns. Domain: `Voucher` aggregate, `VoucherUsage` entity, 4 enums, 2 value objects (`VoucherCode`, `DiscountResult`), 7 domain events. Application: 3 commands + handlers, 4 query types + handlers, `IVoucherRepository`, `IVoucherService`, `CreateVoucherCommandValidator`, `VoucherExpiryJob` (hourly background job). Infrastructure: `PromotionsDbContext` + read context, EF configurations (snake_case columns, value conversions, unique indexes), `VoucherRepository`, `VoucherQuery`, 2 API controllers (CRUD). Integrated into solution: `MarketNest.slnx`, `MarketNest.Web.csproj`, `Program.cs` (MediatR + FluentValidation assembly scan, DbContexts, DI bindings, background job, DatabaseInitializer). Fixed 10 compile errors post-integration (API mismatches vs actual base types). Build: 0 errors, 0 warnings.
- **Notes**: `IVoucherService` (ValidateAsync for checkout apply flow) declared but not implemented — placeholder for when Orders/Cart modules connect. Added `DomainConstants.Currencies` constant (VND default) to `MarketNest.Base.Common`. `VoucherExpiryJob` registered as scoped (not singleton) because it depends on scoped `IVoucherRepository`.

### 2026-04-27 - Voucher & Order Financial Calculation logic integrated into project docs
- **Status**: Completed
- **Description**: Reviewed two new business logic specs (`docs/newlogics/voucher-domain-plan.md`, `docs/newlogics/order-financial-calculation.md`) and merged all logic into authoritative project docs. Updated `domain-and-business-rules.md` (v0.3): added §3.8 Promotions/Voucher aggregate, updated §3.4 Order aggregate with full financial snapshot fields, restructured §3.5 Payment → split Payout into own aggregate (§3.5.1), added §10 Order Financial Calculation Reference with canonical formula, updated invariants (V1–V13, F1–F10), domain events, notification triggers, and value objects. Updated `architecture.md`: added `MarketNest.Promotions` module, `promotions` schema, Redis voucher cache key, Promotions in dependency graph, project count 14→15. Added ADR-015 (Voucher two-axis model) and ADR-016 (Financial calculation two-perspective model) to `decisions.md`.
- **Notes**: Source specs remain in `docs/newlogics/` as reference. See ADR-015 and ADR-016 for design rationale. Phase 1 implementation checklists are in `domain-and-business-rules.md §10.5` and `newlogics/voucher-domain-plan.md` Phase 1 Checklist.

### 2026-04-27 - Loading foundation (skeleton system + checkout overlay + image CLS)
- **Status**: Completed
- **Description**: Phase 1 loading foundation. CSS: `.skeleton-shimmer` (gradient sweep), `.btn-loading`, 4 skeleton shape classes. 4 reusable skeleton partials (`_SkeletonProductCard/StoreCard/OrderRow/StatCard`). Image CLS fix: explicit `width`/`height` on all lazy images. Checkout: Alpine `submitting` state + full-page processing overlay. HTMX: `_SearchInput` inline spinner, `_FilterBar`/`_Pagination` optional `IndicatorId` param.
- **Notes**: Full skeleton-per-page patterns deferred until real DB data is connected. Strategy documented in `docs/frontend-guide.md` §10. See `docs/loading-strategy.md` for full design.

### 2026-04-26 - LoggerMessage refactor complete
- **Status**: Completed
- **Description**: Migrated all production logging from `IAppLogger<T>` dynamic templates to `[LoggerMessage]` source-generated delegates. 50+ files touched. `IAppLogger<T>` stripped to a marker interface (`IAppLogger<T> : ILogger`). `AppLogger<T>` reduced to 3 explicit ILogger members. CA1848/CA2254 suppressions eliminated from production code. EventId registry (`LogEventId` enum) covers all modules with 10,000-block allocations (ADR-033).
- **Notes**: ADR-014. Spec: `docs/superpowers/specs/2026-04-26-loggermessage-refactor-design.md`. Release build: 0 warnings, 0 errors. Architecture tests: 2/2 passed.

### 2026-04-26 - Auditing module foundation
- **Status**: Completed
- **Description**: Created `MarketNest.Auditing` module with automatic audit logging foundation. Two capture points: `AuditableInterceptor` (EF Core SaveChanges hook for `[Auditable]` entities) and `AuditBehavior<,>` (MediatR pipeline for `[Audited]` commands). `IAuditService` contract in Core/Contracts with in-process implementation. Domain: `AuditLog`, `LoginEvent` entities in `auditing` schema. Application: `GetAuditLogsQuery`, `GetLoginEventsQuery` with paged/filterable results. Registered in Program.cs with MediatR assembly scan + DI.
- **Notes**: ADR-012 logged. When building modules, mark entities `[Auditable]` and commands `[Audited("EVENT_TYPE")]` — auditing happens automatically. `AuditableInterceptor` must be added to each module's DbContext options (not AuditingDbContext). Phase 3: swap `AuditService` → `MessageBusAuditService`.

### 2026-04-26 - Starbucks-inspired design system overhaul
- **Status**: Completed
- **Description**: Replaced the "Editorial × Lime" design system with a Starbucks-inspired warm green aesthetic. Key changes: 4-tier green palette (Starbucks Green #00754A, House Green #1E3932, Green Accent #006241, Green Uplift #2b5148) replacing lime accent. Swapped fonts from Geist/Fraunces to DM Sans/Playfair Display/JetBrains Mono (all Google Fonts). Warm cream canvas (#f2f0eb). Starbucks-style layered card shadows. Pill buttons with scale(0.95) active state. Text uses rgba(0,0,0,0.87) instead of pure black. Gold (#cba258) for rewards/premium. Updated ~15 files: input.css, components.css, AppConstants.cs (Colors + Fonts), all 3 layouts, and 6 page-level .cshtml files.
- **Notes**: SoDo Sans (Starbucks proprietary) substituted with DM Sans. Lander Tall substituted with Playfair Display. Dark mode updated to use forest-green tones instead of neutral dark.

### 2026-04-25 - User Settings Architecture spec integrated into project docs
- **Status**: Completed
- **Description**: Reviewed user settings spec (9 tabs, 12+ entities) and integrated into existing docs. Key decisions: distributed settings ownership per module (ADR-011), simplified Phase 1 scope (no FriendsOnly visibility, no NotifyOnSale, no size charts), added cross-module contracts (`IUserPreferencesReadService`, `INotificationPreferenceReadService`). Updated: `domain-and-business-rules.md` (§9), `architecture.md` (module boundaries + schemas), `frontend-guide.md` (settings page inventory + HTMX endpoints), `backend-patterns.md` (contracts + background jobs), `decisions.md` (ADR-011).
- **Notes**: Phase 2 entities (UserSession, UserTwoFactorAuth, PaymentMethod) documented but not implemented. All Phase 1 entities designed to avoid schema changes when Phase 2 features are added.

### 2026-04-25 - Pre-commit secret detection with Gitleaks
- **Status**: Completed
- **Description**: Set up `pre-commit` framework with `gitleaks` hook to block secrets from being committed or pushed. Created `.pre-commit-config.yaml` and `.gitleaks.toml`. Hooks installed for both `pre-commit` and `pre-push` stages.
- **Notes**: ADR-009 logged. Requires `pip install pre-commit` + `pre-commit install` after cloning.

### 2026-04-25 - ADR-007: DDD property accessor convention documented
- **Status**: Completed
- **Description**: Codified DDD property accessor convention across all docs. Entities use `{ get; private set; }`, class-based VOs use `{ get; }`, record-based VOs use `{ get; init; }`, DTOs use `record` with `{ get; init; }`. Infrastructure interfaces (`ISoftDeletable`, `IAuditable`) exempted. Updated `code-rules.md` §3.1, `domain-design.md` §3, `contract-first-guide.md` §4.1, `backend-infrastructure-foundations.md`, `CLAUDE.md`, `AGENTS.md`, and `decisions.md`.
- **Notes**: Existing source code (Entity.cs, Money.cs, Address.cs) already follows the convention — no code changes needed.

### 2026-04-25 - String constants audit: separate config vs code constants
- **Status**: Completed
- **Description**: Audited all string constants in the solution. Removed `SeqFallbackUrl` from `AppConstants.cs` (infrastructure URL belongs in `appsettings.json` only — already present as `Seq:ServerUrl`). Added `SeqServerUrlKey` config key constant. Fixed connection string key mismatch: renamed `appsettings.json` key from `"Default"` to `"DefaultConnection"` to match `AppConstants.DefaultConnectionStringName`. Updated `Program.cs` Seq config to use config key constant with explicit error on missing config.
- **Notes**: All remaining constants confirmed as business logic / coding conventions (roles, routes, colors, error codes, domain validation). See bug log for the connection string mismatch.

### 2026-04-25 - Move system tables from `_system` to `public` schema
- **Status**: Completed
- **Description**: Changed `DatabaseTracker` to use `public` schema instead of `_system` for system tracking tables (`__auto_migration_history`, `__seed_history`). Added `CREATE SCHEMA IF NOT EXISTS` in `DatabaseInitializer` to pre-create each module's schema before migration.
- **Notes**: ADR-006 logged. Module tables remain in their named schemas (e.g., `identity`, `catalog`, `orders`).

### 2026-04-25 - Magic string & magic number elimination sweep
- **Status**: Completed
- **Description**: Scanned entire solution and eliminated hardcoded magic strings/numbers. Created `AppConstants.cs`, `DomainConstants.cs`, `StatusNames.cs` (C#) and `constants.js` (JS). Expanded `AppRoutes.cs` with Admin/Seller/Auth routes. Updated ~25 files across layouts, pages, components, and JS modules.
- **Notes**: Also fixed a bug in `htmxHelpers.js` (wrong login redirect URL). SVG chart colors inside `<stop>` elements left as-is (SVG attributes, not magic strings).

### 2026-04-25 - PR #2: Database initializer foundation
- **Status**: Completed (merged to main)
- **Description**: Added `DatabaseInitializer`, `DatabaseTracker`, `IModuleDbContext`, `ModelHasher`, and `DatabaseServiceExtensions` to bootstrap EF Core per-module migrations on startup
- **PR**: merged via `feature/matthew` → main
- **Notes**: Auto-migration on startup approach — no manual `dotnet ef database update` needed in dev

### 2026-04-25 - PR #1: Frontend base layouts redesign
- **Status**: Completed (merged to main)
- **Description**: Redesigned frontend base layouts with two distinct aesthetics (buyer-facing and seller/admin dashboards)
- **PR**: merged via worktree branch → main
- **Notes**: Seller layout (`_LayoutSeller.cshtml`) and buyer layout are now separated

### 2026-04-25 - feature/foundation: Core infrastructure wiring
- **Status**: In Progress
- **Description**: Wiring up `AssemblyReference`, logging infrastructure, `productForm.js`, and lib assets on the foundation branch
- **Notes**: Branch has several uncommitted modifications — see git status

### 2026-04-25 - feature/foundation: Background job contracts
- **Status**: Planned
- **Priority**: Medium
- **Phase**: Foundation in Phase 1, admin UI in Phase 2, dynamic batch registration in Phase 3+
- **Description**: Admin users should eventually manage timer jobs and batch jobs: view registered jobs, inspect schedules, view execution history/status, retry failed jobs, and register/trigger batch jobs. Full implementation is deferred, but contracts and execution logging should be designed early to avoid inconsistent per-module job implementations as the codebase grows.
- **Decision**: Add shared job contracts (`IBackgroundJob`, `IJobRegistry`, `IJobExecutionStore`) and require all future jobs to expose `JobDescriptor` metadata and execution logs.
- **Risk if ignored**: Each module may implement its own job scheduling/logging/retry pattern, making admin operations and future distributed worker migration expensive.

### 2026-04-26 - Assistant project memory update behavior
- **Status**: Completed
- **Description**: Added a small helper and documentation to ensure project memory (files under `docs/project_notes/`) is updated when edits are made by an agent. Files added: `scripts/log_project_memory.ps1` (PowerShell helper to append entries) and `docs/project_notes/README.md` (how/when to log). From now on, the assistant will offer to append a short entry describing any code or docs change it makes and can run the helper script to persist the note.
- **PR/Issue**: n/a
- **Notes**: This entry documents a behavior change requested by the maintainer. If you prefer automatic commit-time appending, we can add a developer Git hook or CI check; tell me which option you prefer and I will implement it.
---
### 2026-04-26 - Assistant set to prompt before logging
- **Status**: Completed
- **Description**: Assistant will offer to append project memory entries when it modifies code/docs; will run script upon explicit confirmation.
- **Notes**: Logged by assistant via scripts/log_project_memory.ps1

---

### 2026-04-28 - Admin & Configuration Architecture (Phase 1) — Three-Tier Config System
- **Status**: Completed
- **Description**: Implemented the full Phase 1 admin-configuration layer. Key deliverables:
  - **`Base.Domain`**: `ReferenceData` abstract base entity (`Entity<int>`, domain methods for CRUD)
  - **`Base.Common`**: `ICacheService`, `CacheKeys`, `IReferenceDataReadService` + 5 DTOs, 8 Tier 2 contracts (`IOrderPolicyConfig/Writer`, `ICommissionConfig/Writer`, `IStorefrontPolicyConfig/Writer`, `IReviewPolicyConfig/Writer`)
  - **`Admin.Domain`**: 5 reference data entities (Country, Gender, PhoneCountryCode, Nationality, ProductCategory)
  - **`Admin.Infrastructure`**: EF configs (all tables in `public` schema), 5 seeders with embedded JSON data (countries ≥160, phone codes ≥95, genders 4, categories 19), `ReferenceDataReadService` (Redis 24h cache), `AddAdminModule` DI extension
  - **`Orders.Infrastructure`**: `OrdersDbContext`, `OrderPolicyConfig` entity, `OrderPolicyConfigService` (implements both read+write contracts), `AddOrdersModule`
  - **`Payments.Infrastructure`**: `PaymentsDbContext`, `CommissionPolicy` entity (append-only log), `CommissionConfigService` (implements both read+write contracts), `AddPaymentsModule`
  - **`Catalog.Infrastructure`**: In-memory `StorefrontPolicyConfigService` stub + `AddCatalogModule`
  - **`Reviews.Infrastructure`**: In-memory `ReviewPolicyConfigService` stub + `AddReviewsModule`
  - **Stub DI extensions**: Identity, Cart, Disputes, Notifications modules (compile stubs)
  - **`Web/Infrastructure`**: `RedisCacheService` (StackExchange.Redis), `PlatformOptions`, `ValidationOptions`, `SecurityOptions` (Tier 3)
  - **`appsettings.json`**: Added Platform, Validation, Security sections
  - **`Program.cs`**: Wired Redis/ICacheService, Tier 3 Options, OrdersDbContext, PaymentsDbContext, `AddOrdersModule`, `AddPaymentsModule`, updated seeder assemblies
  - **ADRs**: ADR-021 (Three-Tier Config Model), ADR-022 (ReferenceData base in Base.Domain)
- **Build**: `dotnet build` → succeeded ✅
- **PR/Issue**: n/a (inline implementation)
- **Phase 2 TODO**: DB-backed StorefrontPolicyConfig (Catalog) and ReviewPolicyConfig (Reviews); Admin UI pages for commission config + product categories; per-seller commission overrides

