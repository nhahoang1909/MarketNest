# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Shared agent rule files are stored at `agents/rules/` (architecture.md, codestyle.md, git.md, security.md, testing.md). If you update rules, also update `AGENTS.md`.

## Project Overview

MarketNest is a multi-vendor marketplace (Etsy/Shopee-style) built as a solo learning project. The architecture is intentionally phased: **Modular Monolith → Microservices → Kubernetes** over ~9 months.

**Current status**: Phase 1 (Modular Monolith) — actively building. Core kernel, Web host, component library, and infrastructure scaffolding are implemented. Module domain logic (Identity, Catalog, etc.) is in progress.

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

### Agent enforcement: namespace policy

- Before creating or modifying C# files, read `docs/code-rules.md` (section §2.7) and enforce the flat layer-level namespace convention.
- New file namespaces must stop at the layer level. Example:
  - Correct: `namespace MarketNest.Admin.Application;`
  - Incorrect: `namespace MarketNest.Admin.Application.Commands;`
- If you encounter existing files using folder-style namespaces (e.g., `.Commands`), report the mismatch in your change summary and prefer minimal edits to correct only the namespace declaration.
- **Module boundaries**: No module accesses another module's database schema. Cross-module sync calls go through service interfaces; async through domain events.
- Module folder layout vs namespace mapping (summary):
- Modules may use feature sub-folders (e.g., `Modules/Account/Commands`, `Modules/Product/QueryHandlers`) for organization. Keep namespaces at the layer level:
  - `src/MarketNest.Admin/Modules/Account/Commands/CreateAccountCommand.cs` -> `namespace MarketNest.Admin.Application;`
  - `src/MarketNest.Admin/Infrastructure/Persistence/AdminDbContext.cs` -> `namespace MarketNest.Admin.Infrastructure;`
  - Do NOT include `Account`, `Commands`, `Persistence` in the namespace.
- **Immutability**: Records for DTOs and value objects (`{ get; init; }`). Primary constructors for dependency injection. Class-based value objects use `{ get; }` only.
- **No magic strings / magic numbers**: Every repeated string literal and unexplained number must be a `const`, `static readonly`, enum, or bound configuration option. See `docs/code-rules.md` §2.6.
- **English only**: All naming, comments, error messages, log messages, and commit messages must be in English. No Vietnamese or other languages in source code. Localization resource files (`.resx`) are the only exception. See `docs/code-rules.md` §2.1.
- **Architecture tests** (NetArchTest) enforce that presentation layer cannot reference domain directly.
- **Central Package Management**: all NuGet versions are pinned in the repo-root `Directory.Packages.props`. Module `.csproj` files reference packages without versions.
- **Build settings** (`net10.0`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`) are in the repo-root `Directory.Build.props`.
- **Logging**: inject `IAppLogger<T>` (not `ILogger<T>`) and use `[LoggerMessage]` source-generated delegates in a nested `private static partial class Log`. All classes that log must be `partial`. EventIds come from `LogEventId` enum in `MarketNest.Base.Infrastructure/Logging/`. See `docs/code-rules.md` §9 and ADR-014.
- **Route whitelist**: `RouteWhitelistMiddleware` blocks unregistered paths. Add new routes to `AppRoutes` and its `WhitelistedPrefixes` set.
- **Frontend components** live in `src/MarketNest.Web/Pages/Shared/` organized by category: `Data/`, `Display/`, `Domain/`, `Forms/`, `Navigation/`, `Overlays/`. Naming: `_ComponentName.cshtml`. Layouts (`_Layout.cshtml`, `_LayoutAdmin.cshtml`, `_LayoutSeller.cshtml`) also live in `Pages/Shared/`.
- **Event bus**: modules publish integration events via `IEventBus` (`MarketNest.Core/Common/Events/`). Phase 1 uses `InProcessEventBus` (MediatR); Phase 3 swaps to `MassTransitEventBus` (RabbitMQ).
- **Database initialization**: `DatabaseInitializer` auto-migrates and seeds on startup using model hash tracking and PostgreSQL advisory locks. Seeders implement `IDataSeeder` with `Order` and `Version` properties. Each module's `DbContext` must implement `IModuleDbContext`.
- **Auditing**: Mark entities `[Auditable]` for automatic EF Core change tracking; mark commands `[Audited("EVENT_TYPE")]` for automatic MediatR audit logging. `IAuditService` in `Core/Contracts/` — never fails the main request. See ADR-012.

## Agent Behavior Guidelines (rules)

Agent behavior and coding guidelines are maintained under `agents/rules/`.

Please read `agents/rules/README.md` and the rule files in `agents/rules/` (architecture.md, codestyle.md, git.md, security.md, testing.md) for authoritative, agent-facing guidance (Think Before Coding, Simplicity First, Surgical Changes, Goal-Driven Execution) and links to deeper docs (`docs/code-rules.md`, `docs/architecture.md`, etc.).

Note: a single consolidated `agents/GUIDELINES.md` was planned; if present it will be authoritative. For now rely on the `agents/rules/` files and `agents/rules/README.md` which describes the consolidation status.

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
