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
- **Description**: Migrated all production logging from `IAppLogger<T>` dynamic templates to `[LoggerMessage]` source-generated delegates. 50+ files touched. `IAppLogger<T>` stripped to a marker interface (`IAppLogger<T> : ILogger`). `AppLogger<T>` reduced to 3 explicit ILogger members. CA1848/CA2254 suppressions eliminated from production code. EventId registry (`LogEventId` enum) covers all modules with 1000-block allocations.
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

