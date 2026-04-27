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

