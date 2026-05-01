# AGENTS.md

Universal agent instructions for all AI coding assistants working on this repository.
This file is the multi-agent equivalent of `CLAUDE.md` — it applies to Gemini, Codex, Copilot, and any future AI tools.

## Project Overview

MarketNest is a multi-vendor marketplace (Etsy/Shopee-style) — .NET 10, Razor Pages + HTMX + Alpine.js, PostgreSQL (schema-per-module). Phased architecture: **Modular Monolith → Microservices → Kubernetes**.

**Current status**: Phase 1 (Modular Monolith) — actively building. Core kernel, Web host, component library, and infrastructure scaffolding are implemented. Catalog sale-price domain (ADR-024), Promotions/Voucher module, Auditing module, Admin config layer (ADR-021/ADR-022), Roslyn analyzers (MN001–MN036, 36 rules), canonical `BaseQuery`/`BaseRepository` in `Base.Infrastructure` (ADR-025), Unit of Work + `[Transaction]` attribute with pre/post-commit domain event lifecycle (ADR-027), `IRuntimeContext` unified ambient context (ADR-028), Application Constants vs Configuration policy (ADR-030), two-connection-string pattern with `ReadConnection` fallback (ADR-031), `PgQueryBuilder` safe raw SQL utility (ADR-032), Notifications module backend with template-based dispatch (ADR-034), centralized validation infrastructure (`FieldLimits`, `ValidationMessages`, `ValidatorExtensions`) with 14 shared form UI components, `SharedViewPaths` partial-path constants (ADR-035), full UI/HTMX page refactor (Auth forms → HTMX, all pages use `AppRoutes`/`SharedViewPaths`/`FieldLimits`), Trix rich text editor component with HTML sanitization (ADR-036), Excel import/export infrastructure — `IExcelService`/`ClosedXmlExcelProcessor`, `IAntivirusScanner`/`NoOpAntivirusScanner`, `BulkImportVariantsCommand`, seller variant import UI (ADR-037), I18N service wrapper + `I18NKeys` constants (ADR-038), nullable management policy (ADR-039), period-scoped PostgreSQL sequences for running numbers (ADR-040), optimistic concurrency via `IConcurrencyAware` + `UpdateToken` (ADR-041), **Announcement feature foundation — admin-managed site-wide banners with scheduling and HTMX lazy-load display (ADR-043)**, **TrackableInterceptor — automatic audit-trail stamping (CreatedAt/ModifiedAt)** — and common extension method library (`DateTimeOffsetExtensions`, `EnumExtensions`, `StringExtensions`, `NumericExtensions`, `CollectionExtensions` in `Base.Common`) — are implemented. Identity, Cart, Orders, Payments domain logic is in progress.

## Build & Run

```bash
# Backend
dotnet build
dotnet test
dotnet run --project src/MarketNest.Web

# Frontend — Tailwind CSS 4 (run from src/MarketNest.Web/)
npm run build:css        # one-shot minified build
npm run watch:css        # JIT watcher for development

# Infrastructure
docker compose up -d   # uses root-level `docker-compose.yml` (project root)

# EF Core Migrations (auto-applied on startup via DatabaseInitializer)
dotnet ef migrations add <Name> --project src/MarketNest.<Module>
dotnet ef database update

# First-time setup: copy env template and install pre-commit hooks (ADR-009)
# Unix/macOS (or WSL):
cp src/MarketNest.Web/.env.example src/MarketNest.Web/.env   # fill in real values
# Windows PowerShell:
Copy-Item -Path src/MarketNest.Web\.env.example -Destination src/MarketNest.Web\.env
pre-commit install                                            # gitleaks secret detection
```

## Solution Structure

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
  MarketNest.IntegrationTests/
  MarketNest.ArchitectureTests/
```

Each module exposes an `AssemblyReference` marker class for assembly scanning (MediatR, FluentValidation). Cross-module service contracts live in `Base.Common/Contracts/` (namespace `MarketNest.Base.Common`) (e.g., `IAuditService`, `IInventoryService`, `IPaymentService`, `INotificationService`, `IOrderCreationService`, `IStorefrontReadService`, `IUserTimeZoneProvider`, `IHtmlSanitizerService`).

Key types by package (all share namespace matching their project name):
```
Base.Common:
  Cqrs/         ICommand<T>, IQuery<T>, ICommandHandler<,>, IQueryHandler<,>
  Events/       IDomainEvent (integration), IIntegrationEvent, IEventBus + IntegrationEvents/
  Queries/      PagedQuery (base record with Page/PageSize/SortBy/Skip), PagedResult<T>
  Validation/   ValidatorExtensions, ValidationMessages, FieldLimits
  Attributes/   [Auditable], [Audited("EVENT_TYPE")], [Transaction], [NoTransaction]
  ValueObjects/ Address, Money (shared value objects)
  Sequences/    ISequenceService, SequenceDescriptor, SequenceResetPeriod
  Excel/        IExcelService, ExcelTemplate<TRow>, ExcelExportOptions<T>, ExcelUploadRules
  Security/     IAntivirusScanner
  Dtos/         IdAndNameDto, SelectOptionDto<TKey>, DocumentInfo, TimestampDto, StatusDto
  DomainConstants.cs, StatusNames.cs, TableConstants.cs, CacheKeys.cs, SlaConstants.cs
  DateTimeOffsetExtensions.cs, StringExtensions.cs, EnumExtensions.cs, NumericExtensions.cs,
  CollectionExtensions.cs, Result.cs, Error.cs, IDataSeeder.cs

Base.Domain:
  Entity<T>, AggregateRoot, ValueObject, IConcurrencyAware
  Events/  IDomainEvent, IPreCommitDomainEvent, IDomainEventHandler<T>
  ReferenceData (base entity for admin-managed lookup tables)

Base.Infrastructure:
  Persistence/  BaseQuery<T,K,Ctx>, BaseRepository<T,K,Ctx>, IBaseRepository<T,K>,
                IUnitOfWork, IModuleDbContext, DddModelBuilderExtensions,
                PgQueryBuilder, ModelHasher, UpdateTokenInterceptor
  Logging/      IAppLogger<T>, AppLogger<T>, LogEventId enum

Base.Utility:
  BackgroundJobs/  IBackgroundJob, JobDescriptor, IJobRegistry, IJobExecutionStore,
                   JobExecutionContext, JobType, JobTriggerSource
```

The canonical abstract implementations `BaseQuery<TEntity,TKey,TContext>` and `BaseRepository<TEntity,TKey,TContext>` live in `src/Base/MarketNest.Base.Infrastructure/`. Each module provides a 2-line thin wrapper that pins its own `DbContext` type.

Additional top-level directories:
```
skills/                     # AI agent review skills (code review, architecture guard, security, etc.)
infra/nginx/                # Nginx reverse-proxy config
docker-compose.prod.yml     # Production-oriented Compose (root level)
docs/api-contract.md        # Auto-generated from OpenAPI spec by ApiContractGenerator
.gitleaks.toml              # Gitleaks secret detection config (ADR-009)
.pre-commit-config.yaml     # Pre-commit hooks (gitleaks on pre-commit + pre-push)
agents/rules/               # Agent rule files for all assistants: architecture.md, codestyle.md, git.md, security.md, testing.md
```

Frontend static assets in `src/MarketNest.Web/wwwroot/`:
```
css/input.css               # Tailwind CSS 4 source (design tokens, custom properties)
css/components.css          # Extracted component styles
css/site.css                # Built output (generated by npm run build:css)
js/app.js                   # Main JS entry point
js/constants.js             # Shared JS constants
js/components/              # Alpine.js components: confirmDialog, searchBar, starRating, datePicker, imageUploader, infiniteScroll, multiSelect, productForm, reservationTimer, richEditor, loadingOverlay
js/magic/                   # HTMX helper utilities (htmxHelpers.js)
js/stores/                  # Alpine.js stores (cart, toasts, user)
lib/                        # Vendored libraries (alpinejs/, htmx/, chart.js/)
```

## Key Conventions

- See `docs/code-rules.md` for full coding standards
- Use `Result<T, Error>` — never throw for business failures. All CQRS handlers return `Result<T, Error>` via `ICommand<T>` / `IQuery<T>` interfaces in `Base.Common/Cqrs/` (namespace `MarketNest.Base.Common`)
- DDD property accessors (ADR-007): Entity/Aggregate → `{ get; private set; }`, Value Object (class) → `{ get; }`, Value Object (record) → `{ get; init; }`, DTO/Command/Query → `record` with `{ get; init; }`, Infrastructure interfaces → `{ get; set; }` allowed
- **Nullable management (ADR-039)**: `?` is a business decision, not an implementation detail. Every `?` must have a domain-reason comment. Entities use `#pragma warning disable CS8618` on EF Core private constructors only — no `= string.Empty`, `= null!`, `= default!` sentinels anywhere. Value Objects NEVER nullable. DTOs use `required` keyword for required fields. Canonical reference: `docs/nullable-management.md`.
- No magic strings / magic numbers — extract to `const`, `static readonly`, enum, or config options. See `AppConstants` and `AppRoutes` in `src/MarketNest.Web/Infrastructure/` as the canonical examples
  - **AppConstants vs appsettings.json (ADR-030)**: **AppConstants** holds business rules immutable across environments (password length, file limits, colors, fonts). **appsettings.json** holds environment-tunable settings (DB connection, secrets, rate limits, token expiry). Pattern: `AppConstants.Validation.PasswordMinLength` for rules; `Security.RateLimitRequestsPerMinute` in JSON for tuning.
- English only — all naming, comments, error messages, log messages, and commit messages must be in English. No Vietnamese or other languages in source code. Only localization resource files (`.resx`) are exempt. See `docs/code-rules.md` §2.1
- Flat layer-level namespaces: `MarketNest.<Module>.Application`, `MarketNest.<Module>.Domain`, `MarketNest.<Module>.Infrastructure` — sub-folders (Commands/, Queries/, Entities/) do NOT appear in the namespace. See `docs/code-rules.md` §2.7

### Agent enforcement: namespace policy

- Agents MUST read `docs/code-rules.md` (section §2.7) before creating or modifying C# source files and must enforce the flat layer-level namespace rule.
- When generating new files the namespace must stop at the layer level (for example `MarketNest.Admin.Application` or `MarketNest.Admin.Domain`). Do NOT include sub-folder names such as `.Commands`, `.Queries`, `.Entities` in the namespace. Example:
  - Correct: `namespace MarketNest.Admin.Application;`
  - Incorrect: `namespace MarketNest.Admin.Application.Commands;`
- If you find existing files that violate this rule, mention the mismatch in your change summary and propose a minimal fix (preferably editing only the file header namespace) rather than refactoring unrelated code.
- Module boundaries: no cross-schema DB access; use service interfaces (in `Base.Common/Contracts/`) or domain events
- Module folder layout vs namespace mapping:
- Each module's Application layer has two top-level folders: `Common/` (module-wide shared constants, DTOs, audit events, sequences) and `Modules/{Feature}/` (feature-specific CQRS: Commands/, CommandHandlers/, Queries/, QueryHandlers/, Repositories/, Validators/, ImportExport/, Timer/). Infrastructure mirrors with `Queries/Modules/{Feature}/` and `Repositories/Modules/{Feature}/`. When you generate or edit files, keep namespaces flat at the layer level:
  - `src/MarketNest.Catalog/Application/Common/CatalogAuditEvents.cs` → `namespace MarketNest.Catalog.Application;`
  - `src/MarketNest.Catalog/Application/Modules/Variant/Commands/BulkImportVariantsCommand.cs` → `namespace MarketNest.Catalog.Application;`
  - `src/MarketNest.Admin/Application/Modules/Announcement/Commands/CreateAnnouncementCommand.cs` → `namespace MarketNest.Admin.Application;`
  - `src/MarketNest.Admin/Infrastructure/Persistence/AdminDbContext.cs` → `namespace MarketNest.Admin.Infrastructure;`
  - Do NOT include `Common`, `Modules`, `Variant`, `Announcement`, `Commands`, `Persistence` in the namespace.
### Query + Repository Pattern Enforcement

Before writing any handler:
- CommandHandlers MUST inject `I{Entity}Repository`, never `{Module}DbContext` directly
- QueryHandlers for simple reads MUST inject `I{Entity}Query : IBaseQuery<T,K>`
- QueryHandlers for complex reads (pagination, projection, joins) MUST inject a dedicated `IGet{UseCase}Query` interface
- Query interface implementations live in `Infrastructure/Queries/{Feature}/`
- Repository implementations live in `Infrastructure/Repositories/{Feature}/`

Read context rules:
- `{Module}ReadDbContext` is ONLY injected by `BaseQuery` subclasses
- `{Module}DbContext` (write) is ONLY injected by `BaseRepository` subclasses
- Neither context is injected by any Application layer class

Examples in this repository (use these as references):
- `src/Base/MarketNest.Base.Infrastructure/Persistence/BaseQuery.cs` — **canonical** `BaseQuery<TEntity,TKey,TContext>` (in `Base.Infrastructure`); do NOT copy-paste this into modules
- `src/Base/MarketNest.Base.Infrastructure/Persistence/BaseRepository.cs` — **canonical** `BaseRepository<TEntity,TKey,TContext>` (in `Base.Infrastructure`). Write methods: `Add`, `Update`, `Remove` (sync, EF tracking) + `AddRangeAsync`, `UpdateRangeAsync`, `RemoveRangeAsync` (batch, `IEnumerable<TEntity>`). All writes are committed via `IUnitOfWork.CommitAsync` — never call `SaveChangesAsync` directly.
- `src/MarketNest.Admin/Infrastructure/Persistence/BaseQuery.cs` — example 2-line module-local wrapper pinning `AdminReadDbContext`
- `src/MarketNest.Admin/Infrastructure/Persistence/AdminReadDbContext.cs` — the read-only DbContext used by `BaseQuery` implementations
- `src/MarketNest.Admin/Infrastructure/Queries/Modules/Test/TestQuery.cs` and `src/MarketNest.Admin/Infrastructure/Repositories/Test/TestRepository.cs` — concrete implementations wired in the Web host
- Module DI pattern: each module exposes `AddXxxModule(IServiceCollection, IConfiguration)` in `Infrastructure/DependencyInjection.cs` — see Auditing, Promotions, Admin as canonical examples.
- **Auto-registration**: `services.AddModuleInfrastructureServices(params Assembly[])` (in `MarketNest.Web.Infrastructure.ModuleInfrastructureExtensions`) auto-scans assemblies and registers all concrete `IBaseRepository<,>` and `IBaseQuery<,>` implementations as `Scoped`. Add a module's `AssemblyReference` assembly here once it has Query/Repository classes.
- **Connection string strategy (ADR-031)**: Two connection strings only — `DefaultConnection` (write-side DbContexts for all modules) and `ReadConnection` (read-side DbContexts; empty in Phase 1 → fallback to `DefaultConnection`; Phase 2: set to PostgreSQL read replica for zero-code-change scaling). **Never add per-module connection strings** (e.g. `AuditConnection`): module extraction at Phase 3 requires far more than a connection string rename, and ADR-004 schema isolation is the real microservice enabler. Fallback pattern used in every module `DependencyInjection.cs`:
  ```csharp
  string readConnection = configuration.GetConnectionString("ReadConnection")
      is { Length: > 0 } rc ? rc : writeConnection;
  ```

Controller base classes:
- All read controllers extend `ReadApiV1ControllerBase` (no transaction)
- All write controllers extend `WriteApiV1ControllerBase` (`[Transaction]` applied automatically at class level)
- Route prefix: `api/v1/{module}/{resource}` — NOT `api/{module}/{resource}`

- CQRS naming: `PlaceOrderCommand`, `GetOrderByIdQuery`, `OrderPlacedEvent`
- Central Package Management: all NuGet versions are pinned in the repo-root `Directory.Packages.props`. Module `.csproj` files reference packages without versions
- Build settings (`net10.0`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`) are in the repo-root `Directory.Build.props`
- Localization: English (`en`) and Vietnamese (`vi`) via resource files in `src/MarketNest.Web/Resources/` and cookie-based culture provider
- Route whitelist: `RouteWhitelistMiddleware` blocks unregistered paths. Add new routes to `AppRoutes` and its `WhitelistedPrefixes` set
- Frontend components live in `src/MarketNest.Web/Pages/Shared/` organized by category: `Data/`, `Display/`, `Domain/`, `Forms/`, `Navigation/`, `Overlays/`. Naming: `_ComponentName.cshtml`. Layouts (`_Layout.cshtml`, `_LayoutAdmin.cshtml`, `_LayoutSeller.cshtml`) also live in `Pages/Shared/`
- **Shared form field components** — always use the shared partials in `Pages/Shared/Forms/` instead of raw `<input>` tags. They enforce `FieldLimits` at the HTML layer. Available: `_TextField`, `_TextArea`, `_SlugField`, `_EmailField`, `_PhoneField`, `_UrlField`, `_MoneyInput`, `_QuantityInput`, `_StockQuantityInput`, `_PercentageInput`, `_RatingInput`, `_SelectField`, `_ImageUpload`, `_ExcelUpload`, `_SearchInput`, `_RichTextEditor`, `_FormSection`, `_FormActions`. See `docs/common-validation-rules.md` for full usage reference.
- **SharedViewPaths constants (ADR-035)**: All `<partial name="…">` and `Html.PartialAsync(…)` calls for shared components **must** use `SharedViewPaths.*` constants defined in `MarketNest.Web.Infrastructure/SharedViewPaths.cs` — never inline `~/Pages/Shared/…` magic strings. Example: `<partial name="@SharedViewPaths.TextField" …/>`. Add a new constant to `SharedViewPaths` before using any new shared partial.
- **Validation infrastructure**: use `FieldLimits` (field length/range constants), `ValidationMessages` (error message factory), and `ValidatorExtensions` (FluentValidation extensions) from `MarketNest.Base.Common`. `FieldLimits` is available in all Razor views via `_ViewImports.cshtml`. See `docs/common-validation-rules.md`.
- Loading patterns: use `.skeleton-shimmer` + shape classes (`skeleton-card`, `skeleton-text`, `skeleton-avatar`, `skeleton-badge`) from `wwwroot/css/components.css`. Reusable skeleton partials (`_SkeletonProductCard`, `_SkeletonStoreCard`, `_SkeletonOrderRow`, `_SkeletonStatCard`) live in `Pages/Shared/Display/`. Checkout overlay uses Alpine `submitting` state for a full-page processing overlay. HTMX indicators are built into `_SearchInput`, `_FilterBar`, and `_Pagination` via optional `IndicatorId` parameter. See `docs/frontend-guide.md` §10 for the full decision tree
- Logging: inject `IAppLogger<T>` (not `ILogger<T>`) and use `[LoggerMessage]` source-generated delegates in a nested `private static partial class Log`. All classes that log must be `partial`. `IAppLogger<T>` is defined in `src/Base/MarketNest.Base.Infrastructure/Logging/`; EventIds come from the `LogEventId` enum in the same package — each module owns a block of 10,000 IDs (ADR-033). See `docs/code-rules.md` §9 for the complete pattern and per-module EventId block allocation
- Database initialization: `DatabaseInitializer` auto-migrates and seeds on startup using model hash tracking and PostgreSQL advisory locks. Seeders implement `IDataSeeder` with `Order` and `Version` properties
- Each module's `DbContext` must implement `IModuleDbContext` (defines `SchemaName`, `ContextName`). Register via `AddModuleDbContext<TContext>()` in `DatabaseServiceExtensions` so `DatabaseInitializer` can discover all modules
- Event bus: modules publish integration events via `IEventBus` (in `Base.Common/Events/`, namespace `MarketNest.Base.Common`). Phase 1 uses `InProcessEventBus` (MediatR); Phase 3 swaps to `MassTransitEventBus` (RabbitMQ) — transport is a DI swap, module code never references the concrete implementation
- Domain constants: use `DomainConstants` (`Base.Common/DomainConstants.cs`, namespace `MarketNest.Base.Common`) for pagination defaults, validation limits, error codes/messages, date formats, and relative time labels. Use `OrderStatusNames` and `EntityStatusNames` (`Base.Common/StatusNames.cs`) for status string constants
- Value objects `Address` and `Money` live in `Base.Common/ValueObjects/` (namespace `MarketNest.Base.Common`)
- Common shared DTOs live in `Base.Common/Dtos/` (namespace `MarketNest.Base.Common`): `IdAndNameDto`/`IdAndNameIntDto` (minimal lookups), `SelectOptionDto<TKey>`/`SelectOptionDto`/`SelectOptionIntDto` (dropdowns with optional Value/Description/Disabled), `DocumentInfo` (file reference value object with validation), `TimestampDto` (created/updated display), `StatusDto` (status badge Code+Label+Color). Use these instead of defining ad-hoc records in each module.
- FluentValidation extensions: use `ValidatorExtensions` (`Base.Common/Validation/`, namespace `MarketNest.Base.Common`) for reusable rules — `MustBeSlug()`, `MustBePositiveMoney()`, `MustBeNonNegativeMoney()`, `MustBeValidEmail()`, `MustBeValidPhone()`, `MustBeValidId()`, `MustBeValidQuantity()`, `MustBeValidStockQuantity()`, `MustBeValidPercentage()`, `MustBeValidRating()`, `MustBeValidCountryCode()`, `MustBeValidCurrencyCode()`, `MustBeValidTimezone()`, `MustBeValidPostalCode()`, `MustBeInlineStandard()`, `MustBeInlineShort()`, `MustBeMultilineDocument()`, and others. See `docs/common-validation-rules.md` §7. All validators MUST use `ValidationMessages` for error text — no inline string literals.
- Date/time formatting: use `DateTimeOffsetExtensions` for user-local time conversion and relative time strings. User time zone resolved per-request via `IUserTimeZoneProvider` → `HttpContextUserTimeZoneProvider`
- Paged queries: inherit from `PagedQuery` (`Base.Common/Queries/`) — provides `Page`, `PageSize`, `SortBy`, `SortDesc`, `Search`, and `Skip` with built-in validation
- OpenAPI + Scalar: API docs use `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` (not Swagger). Scalar UI available at `/scalar` in dev. `ApiContractGenerator` auto-generates `docs/api-contract.md` from the OpenAPI spec on startup
- Multiple layouts: `_Layout.cshtml` (buyer/public), `_LayoutAdmin.cshtml`, `_LayoutSeller.cshtml` in `src/MarketNest.Web/Pages/Shared/`
- Design tokens: server-side inline color constants live in `AppConstants.Colors` — keep in sync with Tailwind CSS tokens in `wwwroot/css/input.css`
- Auditing: mark entities `[Auditable]` for automatic EF Core change tracking; mark commands `[Audited("EVENT_TYPE")]` for automatic MediatR audit logging — `[Audited]` also accepts `EntityType` (entity name override) and `AuditFailures` (default `true`). `IAuditService` in `Core/Contracts/` — never fails the main request. See ADR-012
- **Sale price on variants (ADR-024)**: `ProductVariant` carries three inline sale fields (`SalePrice`, `SaleStart`, `SaleEnd`). Always use `variant.EffectivePrice()` at checkout / cart reads — never read `Price` directly. `ExpireSalesJob` (Catalog, 5-min schedule) clears expired sales and raises `VariantSalePriceRemovedEvent`. Full rules: `docs/domain-and-business-rules.md` §5.4.
- **Background jobs**: All timer/batch jobs must implement `IBackgroundJob` and expose a `JobDescriptor` with a globally-unique `JobKey` (e.g., `catalog.variant.expire-sales`). Background job contracts (`IBackgroundJob`, `JobDescriptor`, `IJobRegistry`, `IJobExecutionStore`, `JobExecutionContext`) live in `Base.Utility` (namespace `MarketNest.Base.Utility`). `BackgroundJobRunner` (hosted service, polls every 30s) and `ServiceCollectionJobRegistry` live in `src/MarketNest.Web/`. See `docs/backend-patterns.md` §16 for the full list of registered jobs.
- **Raw SQL escape hatch (ADR-032)**: When EF Core cannot express the needed SQL (complex multi-schema joins, DDL, PostgreSQL-specific features), use `PgQueryBuilder` (`Base.Infrastructure/Persistence/PgQueryBuilder.cs`, namespace `MarketNest.Base.Infrastructure`). All values are parameterized (`$1`, `$2`, …). Use `Identifier()` for safe column/table quoting. **Never** pass user input to `Raw()` or `IdentifierRaw()`. `ToDebugString()` is for logging only — never execute its output. See `docs/backend-infrastructure.md` §1.4.
- **Unit of Work (ADR-027)**: Command handlers MUST NOT call `uow.CommitAsync(ct)` or `dbContext.SaveChangesAsync()` directly — the transaction filter calls `uow.CommitAsync()` automatically after the handler returns. `IUnitOfWork` is in `Base.Infrastructure`. **Exception**: background jobs run outside the HTTP pipeline and must call `uow.CommitAsync(ct)` themselves. Domain events split into pre-commit (`IPreCommitDomainEvent` — runs INSIDE TX before SaveChanges) and post-commit (default `IDomainEvent` — dispatched AFTER TX commit, failures logged only). See `docs/backend-patterns.md` §22.
- **RuntimeContext (ADR-028)**: Inject `IRuntimeContext` (in `Base.Common`) instead of `ICurrentUserService` + ad-hoc `HttpContext.TraceIdentifier`. Provides `CorrelationId`, `RequestId`, `CurrentUser` (Id, Name, Email, Role), `StartedAt`, `ElapsedMs`, HTTP metadata. Use `ctx.CurrentUser.RequireId()` in write handlers (throws `UnauthorizedException` if anonymous). Use `ctx.CurrentUser.IdOrNull` in audit interceptors/logging (never throws). Background jobs: `BackgroundJobRuntimeContext.ForSystemJob(jobKey)`. Tests: `TestRuntimeContext.AsSeller()`. See `docs/backend-patterns.md` §23.
- **Transaction filters (ADR-027)**: `RazorPageTransactionFilter` auto-wraps `OnPost*`/`OnPut*`/`OnDelete*`/`OnPatch*` globally — no attribute needed on pages. `TransactionActionFilter` wraps write controller actions when `[Transaction]` is present (inherited from `WriteApiV1ControllerBase`). Opt-out via `[NoTransaction]`. Override isolation: `[Transaction(IsolationLevel.Serializable, timeoutSeconds: 60)]`. Filters in `src/MarketNest.Web/Infrastructure/Filters/`.
- **Excel import/export (ADR-037)**: Use `IExcelService` (contract in `Base.Common/Excel/`) for all import/export — never reference ClosedXML in module code. `ClosedXmlExcelProcessor` is the Web-layer implementation. Import templates use `ExcelTemplate<TRow>` with `Func<string, TRow, Result<Unit, string>>` column setters defined in `<Module>/Application/ImportExport/`. Export uses `ExcelExportOptions<T>`. Column format enum: `ExcelColumnFormat.DecimalNumber` (not `.Decimal`). Template download: `AppRoutes.Seller.ProductImportTemplate`. Import results: `ExcelImportResult<T>` (ValidRows, Errors, TotalRows). Import preview + error display: `SharedViewPaths.ImportPreview`, `SharedViewPaths.ImportErrorTable`. LogEventId block: 150000–159999. See `docs/excel-import-export.md`.
- **Antivirus scanning (ADR-037)**: All file uploads (images AND Excel) MUST pass through `IAntivirusScanner` (contract `Base.Common/Security/IAntivirusScanner.cs`). Phase 1: `NoOpAntivirusScanner` (always clean — dev/internal only). **⚠️ Phase 2: replace with ClamAV binding (`nClam`) via single DI swap** before any public-facing deployment. 4-layer import validation order: (1) extension + magic bytes → (2) antivirus → (3) header validation → (4) row parsing. `ExcelUploadRules` constants in `Base.Common/Excel/` define allowed extensions and magic bytes.
- **I18N service (ADR-038)**: `II18NService` is injected as `I18N` into all Razor views via `_ViewImports.cshtml`. Use `@I18N[I18NKeys.Category.Key]` for static strings and `I18N.Get(key, args)` for parameterized strings. Key constants live in `src/MarketNest.Web/Infrastructure/Localization/I18NKeys.cs`. Never inline localized text directly in `.cshtml` — always add a key to `I18NKeys` and both `.resx` files.
- **Optimistic concurrency (ADR-041)**: Entities that need OCC implement `IConcurrencyAware` and expose an `UpdateToken` (EF row version). Always include `UpdateToken` in update commands and pass it to the repository. EF Core throws `DbUpdateConcurrencyException` on stale-data conflicts.
- **`Service` suffix ban (MN021)**: Concrete classes must NOT use the `Service` suffix unless they directly implement `I{ClassName}Service`. Use `Provider`, `Processor`, `Store`, `Runner`, `Renderer`, or `Sender` instead. Canonical examples: `ClosedXmlExcelProcessor`, `RedisCacheStore`, `TrixHtmlSanitizer`, `PostgresSequenceProvider`, `BackgroundJobRunner`.
- **SLA constants**: `Base.Common/SlaConstants.cs` holds typed threshold constants (`SlaConstants.Performance.SlowRequestMs = 1000`, `CriticalRequestMs = 3000`). `PerformanceBehavior` (MediatR pipeline, registered in Auditing module) logs warnings at these thresholds automatically.

## Agent Behavior Guidelines (rules)

Read `agents/GUIDELINES.md` — the canonical, single source of truth for agent-facing guidance (Think Before Coding, Simplicity First, Surgical Changes, Goal-Driven Execution). It links to authoritative deep docs (`docs/code-rules.md`, `docs/architecture.md`, etc.). The original per-topic rule files are archived under `agents/rules/archive/`.

Using specialized subagents

- This repository exposes a small set of specialized subagents. When a task matches a subagent's responsibility, ALWAYS delegate to it rather than reimplementing the behavior. Use the host's subagent call mechanism (for example, the `run_subagent` tool) to invoke the agent by name.
- Available subagents (current): `Plan` — use this agent for researching and outlining multi-step plans (designs, migration plans, phased work). Example usage: delegate complex, multi-step tasks like migration or extraction plans to `Plan` before making code changes.

Source existing AI conventions

- Before changing code or creating new files, search for and read the project's AI guidance files. Do a glob search for the following filenames and consult any found files: `**/.github/copilot-instructions.md`, `**/AGENT.md`, `**/AGENTS.md`, `**/CLAUDE.md`, `**/.cursorrules/**`, `**/.windsurfrules/**`, `**/.clinerules/**`, `**/.cursor/rules/**`, `**/.windsurf/rules/**`, `**/.clinerules/**`, `**/README.md`.
- In this repository the most relevant files are `CLAUDE.md` (root), `.github/copilot-instructions.md` (root), and `agents/GUIDELINES.md` and `agents/rules/` (see `agents/rules/README.md`). Read them before implementing features so your work matches local conventions.

### Phase-branch PR rule

All pull requests must target a phased feature branch (`p*-main`, e.g. `p1-main`) — **never open a PR directly against `main`**. Create a short-lived feature branch from the current phase branch, then open the PR to that phase branch. Maintainers merge `p*-main` → `main` after phase verification.

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
- All secrets via `.env` file (gitignored) — see `.env.example` in `src/MarketNest.Web/` for key names and defaults

## Specification Documents

All located in `docs/` — read before implementing any feature:

| File | Contents |
|------|----------|
| `docs/architecture.md` | Phased architecture, ADRs, module boundaries, solution structure, project layout |
| `docs/domain-and-business-rules.md` | DDD aggregates, bounded contexts, entity designs, business rules for all modules |
| `docs/backend-patterns.md` | Tech stack, CQRS contracts, `Result<T,Error>`, base classes, services, seeding |
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

---

## Skill Library — AI Agent Skills

> When a task matches a skill below, **always** use `read_file` to load the full SKILL.md
> before proceeding. Skills contain step-by-step workflows, PowerShell scan commands,
> before/after fix templates, and checklists specific to this project.
> Never skip loading the skill — it contains patterns that differ from generic .NET advice.

| Skill | Load When | File |
|---|---|---|
| `dotnet-code-review` | Review C# code, check naming / async / DI / Result / EF Core / HTMX handler patterns | `skills/dotnet-code-review/SKILL.md` |
| `roslyn-analyzer-review` | Build error MN001–MN020, add analyzer rule, write analyzer test, suppress a rule | `skills/roslyn-analyzer-review/SKILL.md` |
| `architecture-guard` | Check layer boundaries, module isolation, DDD aggregate integrity, cross-module access | `skills/architecture-guard/SKILL.md` |
| `database-review` | Review EF Core migrations / N+1 / PostgreSQL indexes / Redis TTL / schema isolation | `skills/database-review/SKILL.md` |
| `security-checks` | Security audit, SQL injection / XSS / IDOR / race condition, OWASP Top 10 | `skills/security-checks/SKILL.md` |
| `performance-optimizer` | Slow code, bottleneck analysis, EF Core query optimization, memory profiling | `skills/performance-optimizer/SKILL.md` |
| `test-quality-check` | Review test quality, xUnit/FluentAssertions/NSubstitute convention, Testcontainers, NetArchTest | `skills/test-quality-check/SKILL.md` |
| `api-contract-review` | HTTP status codes, Problem Details RFC 7807, HTMX patterns, rate limits, antiforgery | `skills/api-contract-review/SKILL.md` |
| `domain-model-review` | DDD aggregates, value objects, state machines, invariants, Result pattern, anemic model | `skills/domain-model-review/SKILL.md` |
| `frontend-code-review` | CSS/HTML/JS quality, accessibility, Core Web Vitals, Alpine.js, Tailwind CSS | `skills/frontend-code-review/SKILL.md` |
| `frontend-htmx-review` | HTMX attributes, hx-swap/hx-trigger/hx-boost patterns, partial responses, anti-flicker | `skills/frontend-htmx-review/SKILL.md` |

### How skills are loaded

```
User asks: "review this command handler"
  → Agent matches: dotnet-code-review
  → Agent calls: read_file("skills/dotnet-code-review/SKILL.md")
  → Agent follows the SCAN → ANALYZE → REPORT → FIX workflow from the skill
  → Agent outputs a structured report with CRITICAL/HIGH/MEDIUM findings
```

The agent should read the skill file **once per session** for a given task. If multiple skills
are relevant (e.g., "review and check security"), load both SKILL.md files before starting.
- Never delete old decisions — future developers need the historical context
