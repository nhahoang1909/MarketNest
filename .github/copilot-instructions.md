# copilot-instruction.md

Copilot agent instructions for coding assistants working on this repository.
This file is the multi-agent equivalent of `CLAUDE.md` — it applies to Gemini, Codex, Copilot, and any future AI tools.

## Project Overview

MarketNest is a multi-vendor marketplace (Etsy/Shopee-style) — .NET 10, Razor Pages + HTMX + Alpine.js, PostgreSQL (schema-per-module). Phased architecture: **Modular Monolith → Microservices → Kubernetes**.

**Current status**: Phase 1 (Modular Monolith) — actively building. Core kernel, Web host, component library, and infrastructure scaffolding are implemented. Catalog sale-price domain (ADR-024), Promotions/Voucher module, Auditing module, Admin config layer (ADR-021/ADR-022), Roslyn analyzers (MN001–MN017), canonical `BaseQuery`/`BaseRepository` in `Base.Infrastructure` (ADR-025), Unit of Work + `[Transaction]` attribute with pre/post-commit domain event lifecycle (ADR-027), `IRuntimeContext` unified ambient context (ADR-028), Application Constants vs Configuration policy (ADR-030), two-connection-string pattern with `ReadConnection` fallback (ADR-031), and `PgQueryBuilder` safe raw SQL utility (ADR-032) are implemented. Identity, Cart, Orders, Payments domain logic is in progress.

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
  MarketNest.Core/          # Shared kernel: Entity<T>, AggregateRoot, ValueObject, Result<T,Error>,
                            #   CQRS interfaces, IDataSeeder, IModuleDbContext, Error codes
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

Each module exposes an `AssemblyReference` marker class for assembly scanning (MediatR, FluentValidation). Cross-module service contracts live in `MarketNest.Core/Contracts/` (e.g., `IAuditService`, `IInventoryService`, `IPaymentService`, `INotificationService`, `IOrderCreationService`, `IStorefrontReadService`, `IUserTimeZoneProvider`).

Core shared sub-packages in `MarketNest.Core/Common/`:
```
Common/Cqrs/            # ICommand<T>, IQuery<T>, handler interfaces
Common/Events/          # IDomainEvent, IIntegrationEvent, IEventBus + IntegrationEvents/ (shared event records)
Common/Persistence/     # IModuleDbContext, IBaseRepository<TEntity,TKey>, ModelHasher
Common/Queries/         # PagedQuery (base record), PagedResult<T>
Common/Validation/      # ValidatorExtensions (MustBeSlug, MustBePositiveMoney, MustBeValidEmail, etc.)
AuditAttributes.cs      # [Auditable] (entity → EF interceptor) + [Audited("EVENT_TYPE")] (command → MediatR behavior)
DomainConstants.cs      # Pagination defaults, validation limits, error codes, date formats, relative time labels
StatusNames.cs          # OrderStatusNames + EntityStatusNames — string constants for status badges
TableConstants.cs       # TableConstants.Schema.* (all module schema names) + TableConstants.SystemTable.* (__auto_migration_history, __seed_history)
DateTimeOffsetExtensions.cs  # User-local time conversion + relative time formatting ("5m ago")
```

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
js/components/              # Alpine.js components: confirmDialog, searchBar, starRating, datePicker, imageUploader, infiniteScroll, multiSelect, productForm, reservationTimer
js/magic/                   # HTMX helper utilities (htmxHelpers.js)
js/stores/                  # Alpine.js stores (cart, toasts, user)
lib/                        # Vendored libraries (alpinejs/, htmx/, chart.js/)
```

## Key Conventions

- See `docs/code-rules.md` for full coding standards
- Use `Result<T, Error>` — never throw for business failures. All CQRS handlers return `Result<T, Error>` via `ICommand<T>` / `IQuery<T>` interfaces in `MarketNest.Core/Common/Cqrs/`
- DDD property accessors (ADR-007): Entity/Aggregate → `{ get; private set; }`, Value Object (class) → `{ get; }`, Value Object (record) → `{ get; init; }`, DTO/Command/Query → `record` with `{ get; init; }`, Infrastructure interfaces → `{ get; set; }` allowed
- **No magic strings / magic numbers**: extract to `const`, `static readonly`, enum, or config options. See `AppConstants` and `AppRoutes` in `src/MarketNest.Web/Infrastructure/` as the canonical examples
  - **AppConstants vs appsettings.json (ADR-030)**: **AppConstants** holds business rules immutable across environments (password length, file limits, colors, fonts). **appsettings.json** holds environment-tunable settings (DB connection, secrets, rate limits, token expiry). Pattern: `AppConstants.Validation.PasswordMinLength` for rules; `Security.RateLimitRequestsPerMinute` in JSON for tuning.
- **English only** — all naming, comments, error messages, log messages, and commit messages must be in English. No Vietnamese or other languages in source code. Only localization resource files (`.resx`) are exempt. See `docs/code-rules.md` §2.1
- Flat layer-level namespaces: `MarketNest.<Module>.Application`, `MarketNest.<Module>.Domain`, `MarketNest.<Module>.Infrastructure` — sub-folders (Commands/, Queries/, Entities/) do NOT appear in the namespace. See `docs/code-rules.md` §2.7

### Agent enforcement: namespace policy

- Agents MUST read `docs/code-rules.md` (section §2.7) before creating or modifying C# source files and must enforce the flat layer-level namespace rule.
- When generating new files the namespace must stop at the layer level (for example `MarketNest.Admin.Application` or `MarketNest.Admin.Domain`). Do NOT include sub-folder names such as `.Commands`, `.Queries`, `.Entities` in the namespace. Example:
  - Correct: `namespace MarketNest.Admin.Application;`
  - Incorrect: `namespace MarketNest.Admin.Application.Commands;`
- If you find existing files that violate this rule, mention the mismatch in your change summary and propose a minimal fix (preferably editing only the file header namespace) rather than refactoring unrelated code.
- Module boundaries: no cross-schema DB access; use service interfaces (in `Core/Contracts/`) or domain events
- Module folder layout vs namespace mapping:
- Modules may contain feature sub-folders (e.g., `Modules/Account/Commands`, `Modules/Product/QueryHandlers`) for organization. When you generate or edit files, keep namespaces at the layer level:
  - `src/MarketNest.Admin/Modules/Account/Commands/CreateAccountCommand.cs` -> `namespace MarketNest.Admin.Application;`
  - `src/MarketNest.Admin/Infrastructure/Persistence/AdminDbContext.cs` -> `namespace MarketNest.Admin.Infrastructure;`
  - Do NOT include `Account`, `Commands`, `Persistence` in the namespace.
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
- `IBaseRepository<TEntity,TKey>` write API: single-entity `Add`/`Update`/`Remove` (sync); batch `AddRangeAsync`/`UpdateRangeAsync`/`RemoveRangeAsync` (async, `IEnumerable<TEntity>`). All writes committed via `IUnitOfWork.CommitAsync()` — never `SaveChangesAsync()` directly.

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
- Logging: use `IAppLogger<T>` (not `ILogger<T>` directly) with `[LoggerMessage]` source-generated delegates in a nested `private static partial class Log`. EventIds come from `LogEventId` enum in `MarketNest.Base.Infrastructure/Logging/` — each module owns a block of 10,000 IDs (ADR-033). See `docs/code-rules.md` §9
- Database initialization: `DatabaseInitializer` auto-migrates and seeds on startup using model hash tracking and PostgreSQL advisory locks. Seeders implement `IDataSeeder` with `Order` and `Version` properties
- Each module's `DbContext` must implement `IModuleDbContext` (defines `SchemaName`, `ContextName`). Register via `AddModuleDbContext<TContext>()` in `DatabaseServiceExtensions` so `DatabaseInitializer` can discover all modules
- Event bus: modules publish integration events via `IEventBus` (in `MarketNest.Core/Common/Events/`). Phase 1 uses `InProcessEventBus` (MediatR); Phase 3 swaps to `MassTransitEventBus` (RabbitMQ) — transport is a DI swap, module code never references the concrete implementation
- Domain constants: use `DomainConstants` (`MarketNest.Core/Common/DomainConstants.cs`) for pagination defaults, validation limits, error codes/messages, date formats, and relative time labels. Use `OrderStatusNames` and `EntityStatusNames` (`MarketNest.Core/Common/StatusNames.cs`) for status string constants
- Value objects `Address` and `Money` live in `MarketNest.Core/ValueObjects/`
- FluentValidation extensions: use `ValidatorExtensions` (`MarketNest.Core/Common/Validation/`) for reusable rules — `MustBeSlug()`, `MustBePositiveMoney()`, `MustBeValidEmail()`, `MustBeValidId()`, `MustBeValidQuantity()`
- Date/time formatting: use `DateTimeOffsetExtensions` for user-local time conversion and relative time strings. User time zone resolved per-request via `IUserTimeZoneProvider` → `HttpContextUserTimeZoneProvider`
- Paged queries: inherit from `PagedQuery` (`MarketNest.Core/Common/Queries/`) — provides `Page`, `PageSize`, `SortBy`, `SortDesc`, `Search`, and `Skip` with built-in validation
- OpenAPI + Scalar: API docs use `Microsoft.AspNetCore.OpenApi` + `Scalar.AspNetCore` (not Swagger). Scalar UI available at `/scalar` in dev. `ApiContractGenerator` auto-generates `docs/api-contract.md` from the OpenAPI spec on startup
- Multiple layouts: `_Layout.cshtml` (buyer/public), `_LayoutAdmin.cshtml`, `_LayoutSeller.cshtml` in `src/MarketNest.Web/Pages/Shared/`
- Design tokens: server-side inline color constants live in `AppConstants.Colors` — keep in sync with Tailwind CSS tokens in `wwwroot/css/input.css`
- Auditing: mark entities `[Auditable]` for automatic EF Core change tracking; mark commands `[Audited("EVENT_TYPE")]` for automatic MediatR audit logging — `[Audited]` also accepts `EntityType` (entity name override) and `AuditFailures` (default `true`). `IAuditService` in `Core/Contracts/` — never fails the main request. See ADR-012
- **Unit of Work (ADR-027)**: Command handlers MUST NOT call `uow.CommitAsync(ct)` or `dbContext.SaveChangesAsync()` directly — the transaction filter (`RazorPageTransactionFilter` / `TransactionActionFilter`) owns the full transaction lifecycle and calls `CommitAsync` automatically after the handler returns. `IUnitOfWork` is in `Base.Infrastructure`. **Exception**: background jobs run outside the HTTP pipeline and must manage transactions explicitly. Domain events split into **pre-commit** (`IPreCommitDomainEvent` — runs INSIDE the open DB transaction before `SaveChanges`) and **post-commit** (plain `IDomainEvent` — dispatched AFTER TX commit; failures logged, never rethrow). See `docs/backend-patterns.md` §22 and ADR-027.
- **RuntimeContext (ADR-028)**: Inject `IRuntimeContext` (contract in `Base.Common`) instead of `ICurrentUserService` + ad-hoc `HttpContext.TraceIdentifier`. Provides `CorrelationId`, `RequestId`, `CurrentUser` (Id, Name, Email, Role), `StartedAt`, `ElapsedMs`, HTTP metadata. Use `ctx.CurrentUser.RequireId()` in write handlers (throws `UnauthorizedException` if anonymous). Use `ctx.CurrentUser.IdOrNull` in audit interceptors/logging (never throws). Background jobs: `BackgroundJobRuntimeContext.ForSystemJob(jobKey)`. Tests: `TestRuntimeContext.AsSeller()`. See `docs/backend-patterns.md` §23 and ADR-028.
- **Transaction filters (ADR-027)**: `RazorPageTransactionFilter` globally wraps `OnPost*`/`OnPut*`/`OnDelete*`/`OnPatch*` automatically. `TransactionActionFilter` wraps write controller actions when `[Transaction]` is present. `WriteApiV1ControllerBase` carries `[Transaction]` at class level. Opt-out: `[NoTransaction]`. In `MarketNest.Web.Infrastructure/Filters/`.
- **Connection string strategy (ADR-031)**: Two connection strings only — `DefaultConnection` (write-side DbContexts for all modules) and `ReadConnection` (read-side DbContexts; empty in Phase 1 → fallback to `DefaultConnection`; Phase 2: set to PostgreSQL read replica for zero-code-change scaling). **Never add per-module connection strings** (e.g. `AuditConnection`): module extraction at Phase 3 requires far more than a connection string rename, and ADR-004 schema isolation is the real microservice enabler. Fallback pattern used in every module `DependencyInjection.cs`:
  ```csharp
  string readConnection = configuration.GetConnectionString("ReadConnection")
      is { Length: > 0 } rc ? rc : writeConnection;
  ```
- **Raw SQL escape hatch (ADR-032)**: When EF Core cannot express the needed SQL (complex multi-schema joins, DDL, PostgreSQL-specific features), use `PgQueryBuilder` (`Base.Infrastructure/Persistence/PgQueryBuilder.cs`, namespace `MarketNest.Base.Infrastructure`). All values are parameterized (`$1`, `$2`, …). Use `Identifier()` for safe column/table quoting. **Never** pass user input to `Raw()` or `IdentifierRaw()`. `ToDebugString()` is for logging only — never execute its output. See `docs/backend-infrastructure.md` §1.4.

Agent behavior and coding guidelines are maintained under `agents/rules/`.

Please read `agents/rules/README.md` and the rule files in `agents/rules/` (architecture.md, codestyle.md, git.md, security.md, testing.md) for authoritative, agent-facing guidance (Think Before Coding, Simplicity First, Surgical Changes, Goal-Driven Execution) and links to deeper docs (`docs/code-rules.md`, `docs/architecture.md`, etc.).

Note: a single consolidated `agents/GUIDELINES.md` was planned; if present it will be authoritative. For now rely on the `agents/rules/` files and `agents/rules/README.md` which describes the consolidation status.

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
| `docs/frontend-guide.md` | Frontend stack, page inventory, HTMX/Alpine patterns, component library, BE-FE contracts |
| `docs/code-rules.md` | Naming conventions, C# idioms, DDD principles, banned patterns |
| `docs/devops-requirements.md` | Docker Compose topology, GitHub Actions, K8s manifests |


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
