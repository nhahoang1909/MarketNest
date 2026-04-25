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
