<div align="center">
<h1>🛒 MarketNest</h1>
<p><strong>A production-grade multi-vendor marketplace</strong> built to evolve from Modular Monolith → Microservices → Kubernetes</p>
<p>
<img src="https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet" alt=".NET 10">
<img src="https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white" alt="PostgreSQL">
<img src="https://img.shields.io/badge/Redis-7-DC382D?logo=redis&logoColor=white" alt="Redis">
<img src="https://img.shields.io/badge/RabbitMQ-3-FF6600?logo=rabbitmq&logoColor=white" alt="RabbitMQ">
<img src="https://img.shields.io/badge/HTMX-2-3D72D7" alt="HTMX">
<img src="https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white" alt="Docker">
<img src="https://img.shields.io/badge/license-MIT-green" alt="MIT License">
</p>
<p><em>Etsy / Shopee-style marketplace — solo learning project demonstrating enterprise backend patterns, DDD, and phased distributed-systems migration</em></p>
<p><strong><a href="#-what-is-this">About</a> · <a href="#-features">Features</a> · <a href="#-architecture">Architecture</a> · <a href="#-tech-stack">Tech Stack</a> · <a href="#-quick-start">Quick Start</a> · <a href="#-documentation">Docs</a></strong></p>
</div>

---

## 🎯 What Is This?

MarketNest is a full-featured multi-vendor marketplace where buyers browse products, sellers manage storefronts, and admins oversee the platform. Built **solo** as a structured learning journey through:

- ✅ **Domain-Driven Design** with bounded contexts, aggregates, and rich domain events
- ✅ **CQRS + MediatR** with a full pipeline behavior chain (validation → audit → performance → transaction)
- ✅ **Custom Roslyn Analyzers** — 33 project-specific rules enforced at build time
- ✅ **Phased architecture** — the same codebase graduates from Docker Compose to Kubernetes without rewriting modules
- ✅ **42+ Architecture Decision Records (ADRs)** — every non-trivial design choice is justified and documented

---

## ✨ Features

### Business Domain

| Module | Capabilities |
|--------|-------------|
| **Identity** | JWT auth, refresh tokens, role-based access (Buyer / Seller / Admin) |
| **Catalog** | Storefront management, product/variant listings, sale-price scheduling, image uploads |
| **Cart** | Session cart with Redis-backed reservation TTLs to prevent overselling |
| **Orders** | Full order lifecycle, fulfillment tracking, financial calculation engine |
| **Payments** | Payment processing, seller payouts, commission calculation |
| **Promotions** | Voucher/coupon engine with usage limits and expiry rules |
| **Reviews** | Star ratings, review votes, seller reputation scoring |
| **Disputes** | Buyer-seller dispute flow, resolution management |
| **Notifications** | Template-based email/in-app dispatch pipeline |
| **Admin** | Back-office config, site-wide announcements with scheduling, job dashboard |
| **Auditing** | Automatic EF Core change tracking + MediatR command audit log |

### Engineering Highlights

#### 🏗️ Modular Monolith with Clean Architecture

Each of the 10 business modules follows a strict 3-layer structure (`Domain` / `Application` / `Infrastructure`) with **schema-per-module** in PostgreSQL — ready for microservice extraction at Phase 3.

```
src/
├── Base/                               # Shared kernel packages
│   ├── MarketNest.Base.Domain/         # Entity<T>, AggregateRoot, ValueObject
│   ├── MarketNest.Base.Common/         # Result<T,Error>, CQRS contracts, shared DTOs
│   ├── MarketNest.Base.Infrastructure/ # BaseQuery, BaseRepository, UoW, logging
│   └── MarketNest.Base.Api/            # ReadApiV1ControllerBase, WriteApiV1ControllerBase
├── MarketNest.{Module}/                # 10 business modules (Identity, Catalog, Cart, …)
└── MarketNest.Web/                     # ASP.NET Core host (composition root, Razor Pages)
```

#### 🤖 Custom Roslyn Analyzer — 33 Project-Specific Rules

A `netstandard2.0` analyzer project (`MarketNest.Analyzers`) enforces architecture rules at **compile time**. Six rules include Quick-Action auto-fixes.

| Category | Example Rules |
|----------|--------------|
| **Naming** | `_camelCase` private fields (MN001), banned suffixes `Manager`/`Helper`/`Impl` (MN002/MN022), CQRS naming convention (MN012–MN015) |
| **Architecture** | Namespace depth limit (MN008), domain layer cannot reference EF/Redis (MN026), no `IQueryable<T>` leaking from repositories (MN027), `init` accessor banned on entities (MN028) |
| **Async** | `async void` banned (MN003), blocking `.Result`/`.Wait()` banned (MN004), fire-and-forget unawaited tasks (MN023) |
| **DDD** | Handler must not inject `DbContext` directly (MN030), `SaveChangesAsync` in handler banned (MN024), query handler missing `AsNoTracking()` (MN029) |
| **Security** | Weak hash algorithms `MD5`/`SHA1` banned (MN018) |

#### ⚡ CQRS Pipeline with Full Behavior Chain

Every command/query passes through a composable MediatR pipeline:

```
Request → ValidationBehavior (FluentValidation)
        → AuditBehavior ([Audited] attribute)
        → PerformanceBehavior (SLA threshold logging)
        → TransactionBehavior (auto UoW commit)
        → Handler
```

#### 🔒 Result\<T, Error\> — No Exceptions for Business Logic

All handlers return `Result<T, Error>` — business failures are values, never thrown exceptions.

```csharp
public Result<Unit, Error> ApplySalePrice(decimal salePrice, DateTimeOffset start, DateTimeOffset end)
{
    if (salePrice >= Price) return Error.Validation("Sale price must be lower than base price");
    if (end <= start)       return Error.Validation("Sale end must be after start");
    SalePrice = salePrice;
    RaiseDomainEvent(new VariantSalePriceSetEvent(Id, salePrice, start, end));
    return Result.Success();
}
```

#### 🔄 Unit of Work + Transaction Filters (ADR-027)

Handlers **never** call `SaveChangesAsync` directly. A global `RazorPageTransactionFilter` wraps all `OnPost*` page handlers; `TransactionActionFilter` wraps API write controllers. Domain events split into:

- **Pre-commit** (`IPreCommitDomainEvent`) — dispatched inside the open transaction for atomic side-effects
- **Post-commit** (`IDomainEvent`) — dispatched after commit for cross-module notifications

#### 📊 Two-Connection-String Strategy (ADR-031)

`DefaultConnection` (write) + `ReadConnection` (read). Phase 1: same DB. Phase 2: `ReadConnection` points to a PostgreSQL read replica — **zero code change required** in any module.

#### 🛡️ Centralized Validation Infrastructure

`FieldLimits`, `ValidationMessages`, and `ValidatorExtensions` provide reusable FluentValidation rules (`MustBeSlug()`, `MustBeValidEmail()`, `MustBePositiveMoney()`, …) — no inline string literals or magic numbers in validators.

---

## 🏛️ Architecture

### Phased Roadmap

```
Phase 1 (Month 1–3)          Phase 2 (Month 4–5)          Phase 3 (Month 6–7)
┌───────────────────┐         ┌───────────────────┐         ┌──────────────────────────┐
│  .NET 10 Monolith │   ──►   │ + Observability   │   ──►   │ YARP API Gateway         │
│  Docker Compose   │         │   (OTel / Seq)    │         │ RabbitMQ + MassTransit   │
│  GitHub Actions   │         │ + Nginx SSL       │         │ Outbox pattern           │
│                   │         │ + Read Replica    │         │ Notification Service     │
└───────────────────┘         └───────────────────┘         └──────────────────────────┘
Phase 4 (Month 8–9)
┌──────────────────────────────────────────────────────┐
│  Kubernetes (kind locally → AKS/EKS)                 │
│  Helm charts · ArgoCD GitOps · HPA auto-scaling      │
└──────────────────────────────────────────────────────┘
```

### Bounded Contexts

```
┌─────────────────────────────────────────────────────────────────┐
│                     MarketNest Platform                         │
│                                                                 │
│  ┌──────────┐  ┌──────────────────┐  ┌───────────────────┐    │
│  │ Identity │  │     Catalog       │  │       Cart        │    │
│  │ User/JWT │  │ Storefront/Product│  │ Redis reservations│    │
│  └──────────┘  └────────┬─────────┘  └────────┬──────────┘    │
│                          │                      │ checkout      │
│                 ┌────────▼──────────────────────▼──────────┐   │
│                 │              Orders                        │   │
│                 │   Order · OrderLine · Fulfillment          │   │
│                 └────┬──────────────────────────────────────┘   │
│          ┌───────────┼────────────┬───────────┐                 │
│  ┌───────▼──┐  ┌─────▼──┐  ┌─────▼──┐  ┌────▼──────────┐      │
│  │ Payments │  │Reviews │  │Disputes│  │  Promotions   │      │
│  └──────────┘  └────────┘  └────────┘  └───────────────┘      │
│                                                                 │
│        Notifications (cross-cutting) · Auditing · Admin         │
└─────────────────────────────────────────────────────────────────┘
```

### Module Internal Structure

```
MarketNest.{Module}/
├── Domain/           # Entities, Aggregates, Value Objects, Domain Events, Enums
├── Application/      # CQRS Commands, Queries, Validators, DTOs, Import/Export
└── Infrastructure/   # EF Core, Redis, Repositories, Queries, Seeders, DI registration
```

---

## 🛠️ Tech Stack

### Backend

| Technology | Version | Role |
|------------|---------|------|
| .NET / ASP.NET Core | **10 LTS** | Runtime + Razor Pages |
| Entity Framework Core | 10 | ORM (PostgreSQL 16) |
| MediatR | 12.x | CQRS mediator + pipeline behaviors |
| FluentValidation | 11.x | Command/query validation |
| MassTransit + RabbitMQ | 8.x | Async messaging (Phase 3+) |
| StackExchange.Redis | 2.x | Cache, session, rate-limit counters |
| Serilog + Seq | 4.x | Structured logging |
| OpenTelemetry | 1.x | Distributed tracing + metrics |

### Frontend

| Technology | Role |
|------------|------|
| Razor Pages | Server-rendered HTML |
| HTMX 2 | Partial page updates without full SPA complexity |
| Alpine.js 3 | Reactive UI components (cart, modals, forms) |
| Tailwind CSS 4 | Utility-first styling with design tokens |
| Chart.js | Seller analytics dashboards |

### Infrastructure (Docker Compose)

| Service | Role |
|---------|------|
| PostgreSQL 16 | Primary datastore (schema-per-module) |
| Redis 7 | Cache + session + rate-limit |
| RabbitMQ 3 | Message broker (Phase 3+) |
| MailHog | Local SMTP + email preview |
| Seq | Structured log viewer |
| Nginx | Reverse proxy + SSL termination |

### Testing

| Tool | Role |
|------|------|
| xUnit + FluentAssertions | Unit & integration tests |
| NSubstitute | Mocking |
| Testcontainers | Real PostgreSQL/Redis in CI |
| NetArchTest | Architecture layer enforcement |

---

## 🚀 Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Node.js 20+](https://nodejs.org/) (for Tailwind CSS build)

### 1. Clone & configure

```bash
git clone https://github.com/nhahoang1909/MarketNest.git
cd MarketNest
# Windows PowerShell
Copy-Item -Path src/MarketNest.Web/.env.example -Destination src/MarketNest.Web/.env
```

### 2. Start infrastructure

```bash
docker compose up -d   # PostgreSQL + Redis + RabbitMQ + MailHog + Seq
```

### 3. Run the app

```bash
dotnet run --project src/MarketNest.Web
# App:     http://localhost:5000
# Seq:     http://localhost:5341
# MailHog: http://localhost:8025
# Scalar:  http://localhost:5000/scalar
```

### 4. Build CSS (optional)

```bash
cd src/MarketNest.Web && npm install && npm run watch:css
```

### 5. Run tests

```bash
dotnet test
```

---

## 📐 Key Engineering Decisions (ADRs)

> 42+ Architecture Decision Records documented in [`docs/architecture.md`](docs/architecture.md) and [`docs/project_notes/decisions.md`](docs/project_notes/decisions.md)

| ADR | Decision | Why It Matters |
|-----|----------|---------------|
| ADR-001 | Modular Monolith first | Prevent premature distributed complexity; boundaries proven before extraction |
| ADR-007 | DDD property accessor policy | `{ get; private set; }` on entities enforces domain invariants via methods only |
| ADR-024 | Sale price inline on `ProductVariant` | Avoids join table; `EffectivePrice()` is the single read path at checkout |
| ADR-025 | Canonical `BaseQuery`/`BaseRepository` | One place for boilerplate; modules cannot drift from the pattern |
| ADR-027 | UoW + Transaction filters | Handlers never call `SaveChangesAsync` — transaction lifecycle owned by infrastructure |
| ADR-028 | `IRuntimeContext` unified ambient context | Single injection for `CorrelationId`, user identity, timing |
| ADR-031 | Two connection strings only | Zero code change when adding a read replica in Phase 2 |
| ADR-032 | `PgQueryBuilder` safe raw SQL | Parameterized values, quoted identifiers — SQL injection impossible by construction |
| ADR-037 | Excel import via `IExcelService` | 4-layer validation: extension → antivirus → header → row parsing |
| ADR-039 | Nullable as a business decision | Every `?` has a domain-reason comment; no `= null!` sentinels |
| ADR-041 | Optimistic concurrency via `UpdateToken` | EF row version; stale-data conflicts surface cleanly |
| ADR-043 | HTMX lazy-load announcements | Banners load after the page without blocking TTFB |

---

## 📂 Project Structure

```
marketnest/
├── src/
│   ├── Base/                       # Shared kernel (Domain, Common, Infrastructure, Api, Utility)
│   ├── MarketNest.{Module}/        # 10 business modules
│   ├── MarketNest.Analyzers/       # Custom Roslyn analyzer (33 rules, 6 code fixes)
│   └── MarketNest.Web/             # ASP.NET Core host
│       ├── Infrastructure/         # AppConstants, AppRoutes, Filters, Middleware, DI
│       ├── Pages/                  # Razor Pages (Buyer, Seller, Admin areas)
│       └── wwwroot/                # Tailwind CSS, HTMX, Alpine.js, Chart.js
├── tests/
│   ├── MarketNest.UnitTests/
│   ├── MarketNest.IntegrationTests/     # Testcontainers — real DB/Redis
│   └── MarketNest.ArchitectureTests/    # NetArchTest layer enforcement
├── docs/                               # 15+ specification documents + ADRs
│   └── project_notes/                  # bugs.md · decisions.md · key_facts.md · issues.md
├── docker-compose.yml                  # Dev infrastructure
└── docker-compose.prod.yml             # Production Compose
```

---

## 🧪 Testing Strategy

| Layer | Tool | What It Tests |
|-------|------|--------------|
| Unit | xUnit + FluentAssertions + NSubstitute | Domain entities, application handlers, validators |
| Integration | Testcontainers (PostgreSQL + Redis) | Repository queries, EF migrations, full command flows |
| Architecture | NetArchTest | Layer boundaries, module isolation, naming conventions |
| Build-time | Custom Roslyn analyzers | 33 coding rules enforced at compile time |

---

## 📖 Documentation

| File | Contents |
|------|----------|
| [`docs/architecture.md`](docs/architecture.md) | Phased architecture, all ADRs, module boundaries, solution layout |
| [`docs/domain-and-business-rules.md`](docs/domain-and-business-rules.md) | DDD aggregates, bounded contexts, business invariants |
| [`docs/backend-patterns.md`](docs/backend-patterns.md) | CQRS contracts, `Result<T,Error>`, base classes, background jobs |
| [`docs/backend-infrastructure.md`](docs/backend-infrastructure.md) | UoW, transactions, caching, PgQueryBuilder, file uploads |
| [`docs/frontend-guide.md`](docs/frontend-guide.md) | HTMX/Alpine patterns, component library, page inventory |
| [`docs/code-rules.md`](docs/code-rules.md) | All naming conventions, DDD principles, banned patterns |
| [`docs/analyzers.md`](docs/analyzers.md) | All 33 Roslyn analyzer rules with suppression patterns |
| [`docs/project_notes/decisions.md`](docs/project_notes/decisions.md) | Full ADR log with context and trade-offs |

---

## 🗺️ Roadmap

- [x] **Phase 1** — Modular Monolith *(in progress)*
  - [x] Core kernel, base packages, `Result<T,Error>` CQRS
  - [x] Identity module (JWT auth, roles)
  - [x] Catalog module (storefronts, products, variants, sale pricing)
  - [x] Promotions / Voucher module
  - [x] Admin back-office (config, announcements)
  - [x] Notifications module (template engine, email dispatch)
  - [x] Auditing module (EF interceptor, MediatR behavior)
  - [x] Custom Roslyn Analyzers (33 rules)
  - [x] Excel import/export infrastructure
  - [x] Optimistic concurrency (`IConcurrencyAware`)
  - [ ] Cart + Orders + Payments domain *(in progress)*
- [ ] **Phase 2** — Observability, integration tests, Nginx SSL, read replica
- [ ] **Phase 3** — YARP API Gateway, RabbitMQ, Outbox pattern, Notification service extraction
- [ ] **Phase 4** — Kubernetes (kind → AKS/EKS), Helm, ArgoCD GitOps

---

## 📝 License

[MIT](LICENSE)

---

<div align="center">
<p><em>Built with ❤️ as a structured journey from Modular Monolith to Microservices to Kubernetes</em></p>
<p><strong>⭐ Star the repo if you find the architecture patterns useful!</strong></p>
</div>
