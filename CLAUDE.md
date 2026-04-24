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

- PostgreSQL: `mn` / `mn_secret`
- Seq (logs): `http://localhost:5341`
- MailHog (email): `http://localhost:8025`
- RabbitMQ management: `http://localhost:15672`
- Health endpoint: `GET /health`
