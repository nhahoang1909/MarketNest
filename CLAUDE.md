# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Shared agent rule files are stored at `agents/rules/` (architecture.md, codestyle.md, git.md, security.md, testing.md). If you update rules, also update `AGENTS.md`.

## Project Overview

MarketNest is a multi-vendor marketplace (Etsy/Shopee-style) built as a solo learning project. The architecture is intentionally phased: **Modular Monolith → Microservices → Kubernetes** over ~9 months.

**Current status**: Phase 1 (Modular Monolith) — actively building. Core kernel, Web host, component library, and infrastructure scaffolding are implemented. Catalog sale-price domain (ADR-024), Promotions/Voucher module, Auditing module, Admin config layer (ADR-021/ADR-022), Roslyn analyzers (MN001–MN036, 36 rules), canonical `BaseQuery`/`BaseRepository` in `Base.Infrastructure` (ADR-025), Unit of Work + `[Transaction]` attribute with pre/post-commit domain event lifecycle (ADR-027), `IRuntimeContext` unified ambient context (ADR-028), Application Constants vs Configuration policy (ADR-030), two-connection-string pattern with `ReadConnection` fallback (ADR-031), `PgQueryBuilder` safe raw SQL utility (ADR-032), Notifications module backend with template-based dispatch (ADR-034), centralized validation infrastructure (`FieldLimits`, `ValidationMessages`, `ValidatorExtensions`) with 14 shared form UI components, `SharedViewPaths` partial-path constants (ADR-035), full UI/HTMX page refactor (Auth forms → HTMX, all pages use `AppRoutes`/`SharedViewPaths`/`FieldLimits`), Trix rich text editor component with HTML sanitization (ADR-036), Excel import/export infrastructure — `IExcelService`/`ClosedXmlExcelProcessor`, `IAntivirusScanner`/`NoOpAntivirusScanner`, `BulkImportVariantsCommand`, seller variant import UI (ADR-037), I18N service wrapper + `I18NKeys` constants (ADR-038), nullable management policy (ADR-039), period-scoped PostgreSQL sequences for running numbers (ADR-040), optimistic concurrency via `IConcurrencyAware` + `UpdateToken` (ADR-041), **Announcement feature foundation — admin-managed site-wide banners with scheduling and HTMX lazy-load display (ADR-043)**, **TrackableInterceptor — automatic audit-trail stamping (CreatedAt/ModifiedAt)** — and common extension method library (`DateTimeOffsetExtensions`, `EnumExtensions`, `StringExtensions`, `NumericExtensions`, `CollectionExtensions` in `Base.Common`) — are implemented. Identity, Cart, Orders, Payments domain logic is in progress.

## Build & Run Commands

```bash
# Backend
dotnet build
dotnet test
dotnet run --project src/MarketNest.Web

# Frontend — Tailwind CSS 4 (run from src/MarketNest.Web/)
npm run build:css        # one-shot minified build
npm run watch:css        # JIT watcher for development

# Infrastructure
docker compose -f src/MarketNest.Web/docker-compose.yml up -d

# EF Core Migrations (auto-applied on startup via DatabaseInitializer)
dotnet ef migrations add <Name> --project src/MarketNest.<Module>
dotnet ef database update
```

## Architecture

### Phase Strategy

- **Phase 1 (Month 1–3)**: Single deployable .NET 10 monolith. Modules communicate via in-process interfaces and MediatR events. One PostgreSQL DB with schema-per-module.
- **Phase 2 (Month 4–5)**: Observability hardening, integration tests (Testcontainers), E2E (Playwright), security audit.
- **Phase 3 (Month 6–7)**: Extract Notification Service. Replace in-process events with RabbitMQ/MassTransit. Add YARP API Gateway and outbox pattern.
- **Phase 4 (Month 8–9)**: Kubernetes (kind locally, AKS/EKS cloud), Helm, ArgoCD GitOps.

### Solution Structure

Solution file: `MarketNest.slnx` (XML-based `.slnx` format, not `.sln`).

```
src/
  Base/                     # Shared cross-project primitives and helper packages
    MarketNest.Base.Api/        # ReadApiV1ControllerBase, WriteApiV1ControllerBase, MapError helper
    MarketNest.Base.Common/     # Result<T,Error>, Error, ICommand<T>/IQuery<T>, IEventBus, IDataSeeder,
                                #   DomainConstants, StatusNames, TableConstants, CacheKeys, SlaConstants,
                                #   FieldLimits, ValidatorExtensions, ValidationMessages,
                                #   ISequenceService, IExcelService, IAntivirusScanner,
                                #   IRuntimeContext + Contracts/ (IAuditService, IInventoryService, …),
                                #   Address/Money ValueObjects, common DTOs, extension methods
    MarketNest.Base.Domain/     # Entity<T>, AggregateRoot, ValueObject, IConcurrencyAware,
                                #   IDomainEvent, IPreCommitDomainEvent, ReferenceData base
    MarketNest.Base.Infrastructure/ # IAppLogger<T>, LogEventId, BaseQuery<T,K,Ctx>, BaseRepository<T,K,Ctx>,
                                    #   IBaseRepository<T,K>, IUnitOfWork, IModuleDbContext,
                                    #   DddModelBuilderExtensions, PgQueryBuilder
    MarketNest.Base.Utility/    # IBackgroundJob, JobDescriptor, IJobRegistry, IJobExecutionStore,
                                #   JobExecutionContext — background-job contracts (namespace MarketNest.Base.Utility)
  MarketNest.Identity/      # Auth module (users, roles, JWT)
  MarketNest.Catalog/       # Storefronts, products, inventory
  MarketNest.Cart/          # Cart, CartItem
  MarketNest.Orders/        # Orders, fulfillment
  MarketNest.Payments/      # Payments, payouts, commission
  MarketNest.Reviews/       # Reviews, votes
  MarketNest.Disputes/      # Disputes, resolution
  MarketNest.Notifications/ # Email/SMS dispatch
  MarketNest.Admin/         # Back-office
  MarketNest.Auditing/      # Audit logs, login events, EF interceptor, MediatR behavior
  MarketNest.Web/           # ASP.NET Core host — composition root, Razor Pages, middleware,
                            #   Infrastructure/ (AppConstants, AppRoutes, DatabaseInitializer)
                            #   Pages/Shared/ (layouts + reusable components by category)
tests/
  MarketNest.UnitTests/
  MarketNest.IntegrationTests/   # WebApplicationFactory + Testcontainers
  MarketNest.ArchitectureTests/  # NetArchTest layer enforcement
```

### Technology Stack

**Backend**: .NET 10, ASP.NET Core, EF Core 10 (PostgreSQL 16), MediatR 12 (CQRS), FluentValidation 11, MassTransit 8 (RabbitMQ), StackExchange.Redis 2, Serilog 4, OpenTelemetry 1, xUnit + FluentAssertions + Testcontainers 3, NetArchTest

**Frontend**: Razor Pages (server-rendered), HTMX 2 (partial page updates), Alpine.js 3 (reactive components), Tailwind CSS 4, Chart.js (seller analytics)

**Infrastructure**: Docker Compose (app + postgres + redis + rabbitmq + mailhog + seq + nginx), GitHub Actions CI/CD

## Key Conventions

All rules are specified in `docs/code-rules.md`. Key items:

- **Error handling**: Use `Result<T, Error>` — never throw for business failures. Exceptions only for truly exceptional infrastructure failures.
- **DDD property accessors (ADR-007)**: Entity/Aggregate properties use `{ get; private set; }` (mutate only via domain methods). Value objects use `{ get; }` (class-based) or `{ get; init; }` (record-based). DTOs/Commands/Queries use `record` with `{ get; init; }`. Infrastructure interfaces (`ISoftDeletable`, `IAuditable`) may use `{ get; set; }`.
- **CQRS naming**: `PlaceOrderCommand`, `GetOrderByIdQuery`, `OrderPlacedEvent` — always explicit, never abbreviated.
- **Flat layer-level namespaces**: Namespaces stop at the layer level — `MarketNest.<Module>.Application`, `MarketNest.<Module>.Domain`, `MarketNest.<Module>.Infrastructure`. Sub-folders (Commands/, Queries/, Entities/) are for file organization only and must NOT appear in the namespace. See `docs/code-rules.md` §2.7.
- **Nullable management (ADR-039)**: `?` is a business decision, not an implementation detail. Every `?` must have a domain-reason comment. Entities use `#pragma warning disable CS8618` on EF Core private constructors; no `= string.Empty`, `= null!` sentinels. Value Objects NEVER nullable. DTOs use `required` keyword, never sentinels. Canonical reference: `docs/nullable-management.md`.

### Agent enforcement: namespace policy

- Before creating or modifying C# files, read `docs/code-rules.md` (section §2.7) and enforce the flat layer-level namespace convention.
- New file namespaces must stop at the layer level. Example:
  - Correct: `namespace MarketNest.Admin.Application;`
  - Incorrect: `namespace MarketNest.Admin.Application.Commands;`
- If you encounter existing files using folder-style namespaces (e.g., `.Commands`), report the mismatch in your change summary and prefer minimal edits to correct only the namespace declaration.
- **Module boundaries**: No module accesses another module's database schema. Cross-module sync calls go through service interfaces; async through domain events.
- Module folder layout vs namespace mapping (summary):
- Each module's Application layer has two top-level folders: `Common/` (module-wide shared constants, DTOs, audit events, sequences) and `Modules/{Feature}/` (feature-specific CQRS: Commands/, CommandHandlers/, Queries/, QueryHandlers/, Repositories/, Validators/, ImportExport/, Timer/). Infrastructure mirrors with `Queries/Modules/{Feature}/` and `Repositories/Modules/{Feature}/`. Keep namespaces flat at the layer level regardless of sub-folder depth:
  - `src/MarketNest.Catalog/Application/Common/CatalogAuditEvents.cs` → `namespace MarketNest.Catalog.Application;`
  - `src/MarketNest.Catalog/Application/Modules/Variant/Commands/BulkImportVariantsCommand.cs` → `namespace MarketNest.Catalog.Application;`
  - `src/MarketNest.Admin/Application/Modules/Announcement/Commands/CreateAnnouncementCommand.cs` → `namespace MarketNest.Admin.Application;`
  - `src/MarketNest.Admin/Infrastructure/Persistence/AdminDbContext.cs` → `namespace MarketNest.Admin.Infrastructure;`
  - Do NOT include `Common`, `Modules`, `Variant`, `Commands`, `Persistence` in the namespace.
- **Immutability**: Records for DTOs and value objects (`{ get; init; }`). Primary constructors for dependency injection. Class-based value objects use `{ get; }` only.
- **No magic strings / magic numbers**: Every repeated string literal and unexplained number must be a `const`, `static readonly`, enum, or bound configuration option. See `docs/code-rules.md` §2.6.
  - **AppConstants vs appsettings.json (ADR-030)**: **AppConstants** holds immutable business rules (password length, file upload limits, colors, font stacks) that never change between environments. **appsettings.json** holds environment-specific settings (connection strings, secrets, rate limits, token expiry) that vary per deployment. Example: `AppConstants.Validation.PasswordMinLength` is in code; `Security.RateLimitRequestsPerMinute` is configurable in JSON.
- **English only**: All naming, comments, error messages, log messages, and commit messages must be in English. No Vietnamese or other languages in source code. Localization resource files (`.resx`) are the only exception. See `docs/code-rules.md` §2.1.
- **Architecture tests** (NetArchTest) enforce that presentation layer cannot reference domain directly.
- **Central Package Management**: all NuGet versions are pinned in the repo-root `Directory.Packages.props`. Module `.csproj` files reference packages without versions.
- **Build settings** (`net10.0`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`) are in the repo-root `Directory.Build.props`.
- **Logging**: inject `IAppLogger<T>` (not `ILogger<T>`) and use `[LoggerMessage]` source-generated delegates in a nested `private static partial class Log`. All classes that log must be `partial`. EventIds come from `LogEventId` enum in `MarketNest.Base.Infrastructure/Logging/` — each module owns a block of 10,000 IDs (ADR-033). See `docs/code-rules.md` §9 and ADR-014.
- **Route whitelist**: `RouteWhitelistMiddleware` blocks unregistered paths. Add new routes to `AppRoutes` and its `WhitelistedPrefixes` set.
- **Frontend components** live in `src/MarketNest.Web/Pages/Shared/` organized by category: `Data/`, `Display/`, `Domain/`, `Forms/`, `Navigation/`, `Overlays/`. Naming: `_ComponentName.cshtml`. Layouts (`_Layout.cshtml`, `_LayoutAdmin.cshtml`, `_LayoutSeller.cshtml`) also live in `Pages/Shared/`.
- **Shared form field components** — always use the shared partials in `Pages/Shared/Forms/` instead of inline `<input>` tags. They enforce `FieldLimits` at the HTML layer. Available: `_TextField`, `_TextArea`, `_SlugField`, `_EmailField`, `_PhoneField`, `_UrlField`, `_MoneyInput`, `_QuantityInput`, `_StockQuantityInput`, `_PercentageInput`, `_RatingInput`, `_SelectField`, `_ImageUpload`, `_ExcelUpload`, `_SearchInput`, `_RichTextEditor`, `_FormSection`, `_FormActions`. See `docs/common-validation-rules.md` for usage.
- **SharedViewPaths constants (ADR-035)**: All `<partial name="…">` and `Html.PartialAsync(…)` calls for shared components **must** use `SharedViewPaths.*` constants defined in `MarketNest.Web.Infrastructure/SharedViewPaths.cs` — never inline `~/Pages/Shared/…` magic strings. Example: `<partial name="@SharedViewPaths.TextField" …/>`. Add a new constant to `SharedViewPaths` before using any new shared partial.
- **Validation infrastructure**: use `FieldLimits` (field length/range constants), `ValidationMessages` (error message factory), and `ValidatorExtensions` (FluentValidation extensions) from `MarketNest.Base.Common`. All three are available in every view via `_ViewImports.cshtml`. See `docs/common-validation-rules.md`.
- **Common shared DTOs** (`Base.Common/Dtos/`, namespace `MarketNest.Base.Common`): `IdAndNameDto`/`IdAndNameIntDto` (minimal lookups), `SelectOptionDto<TKey>`/`SelectOptionDto`/`SelectOptionIntDto` (dropdowns with Value/Description/Disabled), `DocumentInfo` (validated file reference VO), `TimestampDto`, `StatusDto`. Use these across modules — do not define ad-hoc duplicates.
- **Event bus**: modules publish integration events via `IEventBus` (`Base.Common/Events/`, namespace `MarketNest.Base.Common`). Phase 1 uses `InProcessEventBus` (MediatR); Phase 3 swaps to `MassTransitEventBus` (RabbitMQ).
- **Database initialization**: `DatabaseInitializer` auto-migrates and seeds on startup using model hash tracking and PostgreSQL advisory locks. Seeders implement `IDataSeeder` with `Order` and `Version` properties. Each module's `DbContext` must implement `IModuleDbContext`.
- **Auditing**: Mark entities `[Auditable]` for automatic EF Core change tracking; mark commands `[Audited("EVENT_TYPE")]` for automatic MediatR audit logging. `IAuditService` in `Base.Common/Contracts/` — never fails the main request. See ADR-012.
- **Sale price on variants (ADR-024)**: `ProductVariant` carries three inline sale fields (`SalePrice`, `SaleStart`, `SaleEnd`). Always use `variant.EffectivePrice()` at checkout / cart reads — never read `Price` directly. `ExpireSalesJob` (Catalog, 5-min schedule) clears expired sales and raises `VariantSalePriceRemovedEvent`. See §5.4 in `docs/domain-and-business-rules.md`.
- **Background jobs**: All timer/batch jobs must implement `IBackgroundJob` and expose a `JobDescriptor`. Job keys must be globally unique (e.g., `catalog.variant.expire-sales`). Background job contracts live in `Base.Utility` (namespace `MarketNest.Base.Utility`). `BackgroundJobRunner` (hosted service, polls every 30s) and `ServiceCollectionJobRegistry` live in `src/MarketNest.Web/`. See `docs/backend-patterns.md` §16.
- **Auto-registration DI**: `services.AddModuleInfrastructureServices(params Assembly[])` (in `MarketNest.Web.Infrastructure.ModuleInfrastructureExtensions`) auto-registers all `IBaseRepository<,>` and `IBaseQuery<,>` implementations as `Scoped`. Add a module's `AssemblyReference` assembly here once it has Query/Repository classes.
- **Raw SQL escape hatch (ADR-032)**: When EF Core cannot express the needed SQL (complex multi-schema joins, DDL, PostgreSQL-specific features), use `PgQueryBuilder` (`Base.Infrastructure/Persistence/PgQueryBuilder.cs`). All values are parameterized (`$1`, `$2`, …). Use `Identifier()` for safe column/table quoting. **Never** pass user input to `Raw()` or `IdentifierRaw()`. `ToDebugString()` is for logging only — never execute its output. See `docs/backend-infrastructure.md` §1.4.
- **Base query/repository (ADR-025)**: canonical `BaseQuery<TEntity,TKey,TContext>` and `BaseRepository<TEntity,TKey,TContext>` live in `Base.Infrastructure`. Each module declares a 2-line thin wrapper that pins its own `DbContext`. Never copy the base logic into a module — inherit it. Rule: CommandHandlers inject `I{Entity}Repository`; QueryHandlers inject `I{Entity}Query : IBaseQuery<T,K>` or a dedicated `IGet{UseCase}Query` for complex reads. Neither `{Module}DbContext` nor `{Module}ReadDbContext` is injected by any Application layer class. `IBaseRepository<TEntity,TKey>` exposes single-entity write methods (`Add`, `Update`, `Remove`) and batch methods (`AddRangeAsync`, `UpdateRangeAsync`, `RemoveRangeAsync` — accept `IEnumerable<TEntity>`). All writes are flushed via `IUnitOfWork.CommitAsync()` — never call `SaveChangesAsync` directly.
- **Unit of Work (ADR-027)**: Command handlers MUST NOT call `uow.CommitAsync(ct)` or`dbContext.SaveChangesAsync()` directly — the transaction filter calls `uow.CommitAsync()` automatically after the handler returns. `IUnitOfWork` is defined in `Base.Infrastructure`. **Exception**: background jobs run outside the HTTP pipeline and must call `uow.CommitAsync(ct)` themselves. Domain events split into two types: **pre-commit** (`IPreCommitDomainEvent` — dispatched INSIDE the open DB transaction before `SaveChanges`, for atomic side effects) and **post-commit** (plain `IDomainEvent` — dispatched AFTER transaction commit, failures logged only). `UnitOfWork` implementation in `MarketNest.Web.Infrastructure/Persistence/`. See `docs/backend-patterns.md` §22 and ADR-027.
- **Transaction filters (ADR-027)**: `RazorPageTransactionFilter` (globally registered) auto-wraps `OnPost*`/`OnPut*`/`OnDelete*`/`OnPatch*` Razor Page handlers in a DB transaction — no attribute needed on pages. `OnGet*` always bypassed. `TransactionActionFilter` (globally registered) wraps controller actions only when `[Transaction]` is present on class or action — `WriteApiV1ControllerBase` carries `[Transaction]` at class level. Opt-out: `[NoTransaction]`. Override isolation: `[Transaction(IsolationLevel.Serializable, timeoutSeconds:60)]`. Filters in `MarketNest.Web.Infrastructure/Filters/`.
- **Controller base classes (ADR-027)**: `ReadApiV1ControllerBase` for GET-only controllers; `WriteApiV1ControllerBase` for POST/PUT/DELETE/PATCH controllers (applies `[Transaction]` automatically). Both in `Base.Api`.
- **Module DI pattern**: each module exposes `AddXxxModule(IServiceCollection, IConfiguration)` in `Infrastructure/DependencyInjection.cs`. Canonical examples: Admin, Auditing, Promotions, Orders, Payments. After registering module DI, call `services.AddModuleInfrastructureServices(moduleAssembly)` to auto-register the module's Query/Repository implementations.
- **Connection string strategy (ADR-031)**: Two connection strings only — `DefaultConnection` (write-side DbContexts for all modules) and `ReadConnection` (read-side DbContexts; empty in Phase 1 → fallback to `DefaultConnection`; Phase 2: set to PostgreSQL read replica for zero-code-change scaling). **Never add per-module connection strings** (e.g. `AuditConnection`): module extraction at Phase 3 requires far more than a connection string rename, and ADR-004 schema isolation is the real microservice enabler. Fallback pattern used in every module `DependencyInjection.cs`:
  ```csharp
  string readConnection = configuration.GetConnectionString("ReadConnection")
      is { Length: > 0 } rc ? rc : writeConnection;
  ```
- **Excel import/export (ADR-037)**: Use `IExcelService` (contract in `Base.Common/Excel/`) for all import and export operations — never reference ClosedXML directly in module code. `ClosedXmlExcelProcessor` is the Web-layer implementation (registered as `Scoped`). Import templates use `ExcelTemplate<TRow>` with typed `Func<string, TRow, Result<Unit, string>>` setter callbacks — all column definitions live in `<Module>/Application/ImportExport/`. Export uses `ExcelExportOptions<T>` with `ExcelColumnExport<T>` column definitions. Template downloads served from `AppRoutes.Seller.ProductImportTemplate`. Column format enum: use `ExcelColumnFormat.DecimalNumber` (not `.Decimal`).
- **Antivirus scanning (ADR-037)**: All file uploads (images AND Excel) MUST pass through `IAntivirusScanner` (contract in `Base.Common/Security/`) before processing. Phase 1: `NoOpAntivirusScanner` (always clean — acceptable for dev/internal). **⚠️ Phase 2: replace with ClamAV binding (`nClam` or clamd socket) via single DI swap** — never skip this for a public-facing deployment. 4-layer import validation: (1) extension + magic bytes → (2) antivirus scan → (3) header validation → (4) row-level parsing. See `docs/excel-import-export.md`.
- **I18N service (ADR-038)**: `II18NService` is injected as `I18N` into all Razor views via `_ViewImports.cshtml`. Use `@I18N[I18NKeys.Category.Key]` for static strings and `I18N.Get(key, args)` for parameterized strings. Key constants live in `src/MarketNest.Web/Infrastructure/Localization/I18NKeys.cs`. Never inline localized text directly in `.cshtml` — always add a key to `I18NKeys` and the `.resx` resource files.
- **Optimistic concurrency (ADR-041)**: Entities that need OCC implement `IConcurrencyAware` and expose an `UpdateToken` (EF row version). Always include `UpdateToken` in update commands and pass it to the repository. EF Core automatically throws `DbUpdateConcurrencyException` on stale-data conflicts.
- **`Service` suffix ban (MN021)**: Concrete classes must NOT use the `Service` suffix unless they directly implement an interface named `I{ClassName}Service`. Use `Provider`, `Processor`, `Store`, `Runner`, `Renderer`, or `Sender` instead. Example canonical renames: `ClosedXmlExcelProcessor` (was `ClosedXmlExcelService`), `RedisCacheStore`, `TrixHtmlSanitizer`, `PostgresSequenceProvider`, `BackgroundJobRunner`.
- **SLA constants**: `Base.Common/SlaConstants.cs` exposes typed threshold constants (`SlaConstants.Performance.SlowRequestMs = 1000`, `CriticalRequestMs = 3000`, etc.). `PerformanceBehavior` (MediatR pipeline, registered in Auditing module) logs warnings at these thresholds automatically — no per-handler timing code needed.

## Agent Behavior Guidelines (rules)

Read `agents/GUIDELINES.md` — the canonical, single source of truth for agent-facing guidance (Think Before Coding, Simplicity First, Surgical Changes, Goal-Driven Execution). It links to authoritative deep docs (`docs/code-rules.md`, `docs/architecture.md`, etc.). The original per-topic rule files are archived under `agents/rules/archive/`.

**Using specialized subagents**: When a task matches a subagent's responsibility, ALWAYS delegate using the `run_subagent` tool. Available subagents: `Plan` — use for researching and outlining multi-step plans (designs, migration plans, phased work).

## Specification Documents

All located in `docs/` — read before implementing any feature:

| File | Contents |
|------|----------|
| `docs/architecture.md` | Phased architecture, ADRs, module boundaries, solution structure, project layout |
| `docs/domain-and-business-rules.md` | DDD aggregates, bounded contexts, entity designs, business rules for all modules |
| `docs/backend-patterns.md` | Tech stack, CQRS contracts, `Result<T,Error>`, base classes, services, seeding, background jobs |
| `docs/backend-infrastructure.md` | Query utilities, caching, transactions, UoW, `[Access]` permissions, file uploads, testing |
| `docs/caching-strategy.md` | Four-layer caching (static assets, OutputCache, Redis, cross-module), cache keys, invalidation, anti-patterns |
| `docs/notifications.md` | Notifications module: template engine, dispatch pipeline, email/in-app channels, usage guide |
| `docs/frontend-guide.md` | Frontend stack, page inventory, HTMX/Alpine patterns, component library, BE-FE contracts |
| `docs/code-rules.md` | Naming conventions, C# idioms, DDD principles, banned patterns |
| `docs/common-validation-rules.md` | FieldLimits, ValidationMessages, ValidatorExtensions — centralized validation infrastructure |
| `docs/common-extension-methods.md` | All extension methods: DateTimeOffset, Enum, String (IsValidEmail/Phone/Slug/Url/…), Numeric, Collection |
| `docs/excel-import-export.md` | Excel import/export: IExcelService, ClosedXML, antivirus hook, import templates, export options, ADR-037 |
| `docs/sequence-service.md` | PostgreSQL sequence service: ISequenceService, period-scoped sequences, running number generation, ADR-040 |
| `docs/nullable-management.md` | Nullable management policy: `?` as a business decision, entity constructor pragma, banned sentinels, ADR-039 |
| `docs/devops-requirements.md` | Docker Compose topology, GitHub Actions, K8s manifests |
| `docs/analyzers.md` | Roslyn analyzer reference: all 33 MN rules, suppression patterns, adding new rules |
| `docs/test-driven-design.md` | TDD guidelines, unit/integration/architecture test patterns |
| `docs/sla-requirements.md` | SLA requirements — availability, performance, business correctness, data integrity thresholds (ADR-026) |
| `docs/extension-methods-cheatsheet.md` | Quick reference card for all extension methods (bookmark for everyday use) |

## Phase 1 Exit Criteria (Month 3)

A real user can: browse → register → create storefront → list product → another user buys it → order fulfilled. Deployed on a VPS via Docker Compose.

## Infrastructure Defaults (dev)

- PostgreSQL `16-alpine`: port `5432`, user `mn`, database `marketnest`
- Redis `7-alpine`: port `6379`
- RabbitMQ `3-management-alpine`: ports `5672` / `15672` (management UI)
- MailHog: SMTP `1025`, web UI `8025`
- Seq (structured logs): `http://localhost:5341`
- App: `http://localhost:5000` → container `8080`
- Health endpoint: `GET /health`
- All secrets via `.env` file (gitignored) — see `appsettings.json` for key names

## Project Memory System

This project maintains institutional knowledge in `docs/project_notes/` for consistency across AI sessions and human developers.

### Memory Files — What Goes Where

| File | Purpose | Example entries |
|------|---------|----------------|
| **bugs.md** | Bug log with dates, root cause, solution, and prevention | Runtime errors, config mistakes, migration failures |
| **decisions.md** | Architectural Decision Records (ADRs) with context & trade-offs | Tech choices, pattern choices, library picks |
| **key_facts.md** | Non-sensitive project config: ports, URLs, namespaces, env names | `PostgreSQL: 5432`, `Seq: http://localhost:5341` |
| **issues.md** | Work log with PR references and brief descriptions | Completed PRs, in-progress features |

### Memory-Aware Protocols

**Before proposing architectural changes:**
- Check `docs/project_notes/decisions.md` for existing decisions
- Verify the proposed approach doesn't conflict with past choices
- If it conflicts, acknowledge the prior ADR and explain why revisiting is warranted
- Mark superseded decisions with `**Status**: Superseded by ADR-XXX` — never delete old ADRs

**When encountering errors or bugs:**
- Search `docs/project_notes/bugs.md` for similar issues
- Apply known solutions if found
- Document new bugs and solutions when resolved using the format:
  ```
  ### YYYY-MM-DD - Bug Title
  - **Issue**: What happened
  - **Root Cause**: Why it happened
  - **Solution**: How you fixed it
  - **Prevention**: How to avoid it
  ```
- Keep entries scannable in 30 seconds — link to separate docs for lengthy details

**When looking up project configuration:**
- Check `docs/project_notes/key_facts.md` for credentials references, ports, URLs
- Prefer documented facts over assumptions

**When completing work on a PR or feature:**
- Log completed work in `docs/project_notes/issues.md` with date, description, and PR link

**When user requests memory updates:**
- Route to the correct file: bugs → `bugs.md`, decisions → `decisions.md`, config → `key_facts.md`, work → `issues.md`
- Follow the established format (structured entries, dates, concise text)

### Secrets Policy — What NEVER Goes in `key_facts.md`

> **Rule**: Secrets belong in `.env` (gitignored), cloud secrets managers, or CI/CD variables — **never** in version-controlled markdown files.

| ❌ Never store in `key_facts.md` | ✅ Safe to store in `key_facts.md` |
|---|---|
| Passwords, API keys, access tokens | Hostnames and public URLs |
| Service account keys / private keys | Port numbers (e.g., `5432`, `6379`) |
| OAuth client secrets, refresh tokens | Project identifiers, environment names |
| DB connection strings with passwords | Non-sensitive config (timeouts, retry counts) |
| SSH keys, VPN credentials | Service account email addresses |

**Where secrets belong:**
- **`.env` files** (gitignored) — local development (`DATABASE_PASSWORD=secret123`)
- **Cloud secrets managers** — production (GCP Secret Manager, AWS Secrets Manager, Azure Key Vault)
- **CI/CD variables** — pipelines (GitHub Actions secrets, GitLab CI/CD variables)
- **Kubernetes Secrets** — containerized deployments

> ⚠️ If secrets are accidentally committed, **rotate them immediately**. Removing from git history isn't enough — the repo may already be cloned elsewhere.

### Memory Maintenance

**Keep entries concise:**
- Each bug/decision/issue should be scannable in 30 seconds
- Use the structured format — if more detail is needed, link to a separate document

**Make updates reflexive, not deliberate:**
- After every bug fix → "Log this in `bugs.md`"
- After every architecture discussion → "Add an ADR in `decisions.md`"
- After every PR merge → "Update `issues.md`"

**Scaling (when files grow large):**
- When any file exceeds ~20 entries, add a Table of Contents at the top
- For `bugs.md` and `issues.md`: archive entries older than 6–12 months to `bugs-archive-YYYY.md` / `issues-archive-YYYY.md`, keeping a reference in the main file
- `decisions.md` and `key_facts.md` do **not** get archived — they remain relevant indefinitely (mark outdated decisions as superseded)

**Review cadence:**
- Review `decisions.md` quarterly — mark stale ADRs as `**Status**: Superseded by ADR-XXX`
- Never delete old decisions — future developers need the historical context
