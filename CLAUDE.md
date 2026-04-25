# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MarketNest is a multi-vendor marketplace (Etsy/Shopee-style) built as a solo learning project. The architecture is intentionally phased: **Modular Monolith → Microservices → Kubernetes** over ~9 months.

**Current status**: Planning phase — all 14 specification documents are complete, no source code yet. Implementation begins with Phase 1 (Modular Monolith).

## Build & Run Commands

Once source code exists (Phase 1):

```bash
# Backend
dotnet build
dotnet test
dotnet run --project src/MarketNest.Web

# Frontend (Tailwind JIT)
npm run build:tailwind

# Infrastructure (Docker Compose)
docker compose up -d

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

### Solution Structure (Phase 1 target)

```
src/
  MarketNest.Core/          # Shared kernel: base classes, value objects, Result<T,Error>
  MarketNest.Identity/      # Auth: users, roles, JWT, refresh tokens
  MarketNest.Catalog/       # Storefronts, products, product variants, inventory
  MarketNest.Cart/          # Cart, CartItem, Redis-backed reservation service
  MarketNest.Orders/        # Orders, order lines, fulfillment, shipment state machine
  MarketNest.Payments/      # Payments, payouts, commission (IPaymentGateway abstraction)
  MarketNest.Reviews/       # Reviews, votes, fraud gate
  MarketNest.Disputes/      # Disputes, messages, resolution
  MarketNest.Notifications/ # Email/SMS dispatch (in-process Phase 1, standalone Phase 3)
  MarketNest.Admin/         # Back-office: arbitration, platform config
  MarketNest.Web/           # ASP.NET Core host: Razor Pages + minimal API endpoints
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
- **CQRS naming**: `PlaceOrderCommand`, `GetOrderByIdQuery`, `OrderPlacedEvent` — always explicit, never abbreviated.
- **Module boundaries**: No module accesses another module's database schema. Cross-module sync calls go through service interfaces; async through domain events.
- **Immutability**: Records for DTOs and value objects. Primary constructors for dependency injection.
- **No magic strings / magic numbers**: Every repeated string literal and unexplained number must be a `const`, `static readonly`, enum, or bound configuration option. See `docs/code-rules.md` §2.5.
- **Architecture tests** (NetArchTest) enforce that presentation layer cannot reference domain directly.

## Specification Documents

All located in `docs/` — read before implementing any feature:

| File | Contents |
|------|----------|
| `docs/architecture-requirements.md` | Phased architecture, ADRs, module boundary rules |
| `docs/backend-requirements.md` | Full tech stack, solution structure, CQRS patterns |
| `docs/frontend-requirements.md` | Frontend stack rationale, complete page inventory |
| `docs/code-rules.md` | Naming conventions, C# idioms, DDD principles, banned patterns |
| `docs/domain-design.md` | DDD aggregates, bounded contexts, entity designs |
| `docs/contract-first-guide.md` | CQRS marker interfaces, `Result<T,Error>`, event contracts |
| `docs/business-logic-requirements.md` | Business rules for all modules |
| `docs/backend-infrastructure-foundations.md` | Base classes, `DatabaseInitializer`, `IDataSeeder` |
| `docs/database-infrastructure-utilities.md` | Query builders, specifications, background jobs |
| `docs/be-fe-common-services.md` | HTMX/Alpine integration, HX-Trigger events, form conventions |
| `docs/frontend-component-library.md` | Component registry, form fields, Alpine magic helpers |
| `docs/devops-requirements.md` | Docker Compose topology, GitHub Actions, K8s manifests |
| `docs/advanced-patterns-transaction-auth-fileupload.md` | Saga patterns, auth flows, file uploads |
| `docs/project-planning.md` | Phase timelines, weekly tasks, Phase 1 exit criteria |

## Phase 1 Exit Criteria (Month 3)

A real user can: browse → register → create storefront → list product → another user buys it → order fulfilled. Deployed on a VPS via Docker Compose.

## Infrastructure Defaults (dev)

- PostgreSQL: user `mn`, database `marketnest` (password in `.env` — see `.env.example`)
- Seq (logs): `http://localhost:5341`
- MailHog (email): `http://localhost:8025`
- RabbitMQ management: `http://localhost:15672`
- Health endpoint: `GET /health`
- All secrets managed via `.env` file (gitignored) — copy `.env.example` to `.env` for setup

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
