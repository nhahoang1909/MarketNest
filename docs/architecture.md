# MarketNest — Architecture & Project Structure

> Version: 0.1 (Planning) | Status: Draft | Date: 2026-04
> Consolidated from: `architecture-requirements.md` + `project-structure.md`

---

## Table of Contents

1. [Overview & Goals](#1-overview--goals)
2. [Phased Architecture Strategy](#2-phased-architecture-strategy)
3. [Architecture Decisions (ADRs)](#3-architecture-decisions-adrs-summary)
4. [Solution Structure](#4-solution-structure)
5. [Module Boundaries](#5-module-boundaries)
6. [Data Architecture](#6-data-architecture)
7. [Infrastructure Architecture](#7-infrastructure-architecture)
8. [Observability Stack](#8-observability-stack)
9. [Security Architecture](#9-security-architecture)
10. [Testing Strategy](#10-testing-strategy)
11. [Dependency Graph](#11-dependency-graph)
12. [Quick Start Commands](#12-quick-start-commands)
13. [Open Questions / Risks](#13-open-questions--risks)

---

## 1. Overview & Goals

MarketNest is a **multi-vendor marketplace** (Etsy/Shopee mini) built as a learning project to progressively evolve from **Monolith → Microservices**, covering full-stack, backend, and DevOps practices in a real-world business context.

### Non-Functional Goals

| Goal | Target |
|------|--------|
| Learning coverage | FE + BE + DDD + DevOps + Distributed Systems |
| Timeline | 6–9 months (phased) |
| Team size | 1 developer (solo) |
| Deployment target | Docker Compose (Phase 1–2) → K8s (Phase 4) |
| Availability | Best-effort (toy project, no SLA) |
| Scalability | Handle ~100 concurrent users in prod-like environment |

---

## 2. Phased Architecture Strategy

```
Phase 1: Modular Monolith        Phase 3: First Service Split
┌─────────────────────────┐      ┌──────────────────────────────────┐
│     .NET 10 Monolith    │      │  API Gateway (YARP)              │
│  ┌───────────────────┐  │      │  ┌──────────────┐  ┌──────────┐ │
│  │ Storefront Module │  │  ──► │  │  Monolith    │  │ Notif.   │ │
│  │ Order Module      │  │      │  │  Core        │  │ Service  │ │
│  │ Payment Module    │  │      │  └──────────────┘  └──────────┘ │
│  │ Notification Mod. │  │      │          │              │        │
│  └───────────────────┘  │      │       RabbitMQ ◄────────┘        │
└─────────────────────────┘      └──────────────────────────────────┘

Phase 4: Kubernetes
┌──────────────────────────────────────────────────────────────────┐
│  Ingress (Nginx)                                                 │
│  ┌────────────────────────────────────────────┐                 │
│  │  YARP API Gateway  (HPA enabled)           │                 │
│  └────────────────────────────────────────────┘                 │
│       │              │              │                            │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐                       │
│  │ Core     │  │ Notif.   │  │ (future) │                       │
│  │ Service  │  │ Service  │  │ Payment  │                       │
│  │ (3 pods) │  │ (2 pods) │  │ Service  │                       │
│  └──────────┘  └──────────┘  └──────────┘                       │
│  PostgreSQL  Redis  RabbitMQ  (StatefulSets)                     │
└──────────────────────────────────────────────────────────────────┘
```

| Phase | Period | Infrastructure | Goal |
|-------|--------|---------------|------|
| Phase 1 | Month 1–3 | Docker Compose + GitHub Actions | Ship something deployable; define background job contracts and execution logging foundation |
| Phase 2 | Month 4–5 | + Nginx SSL + Observability | Production-grade monolith; add admin job dashboard, execution history, failure inspection, and retry |
| Phase 3 | Month 6–7 | + YARP Gateway + RabbitMQ | Distributed systems basics; queue-backed batch jobs and worker execution |
| Phase 4 | Month 8–9 | K8s (kind → AKS/EKS) + ArgoCD | Operate a real cluster; distributed scheduling and job execution locks |

---

## 3. Architecture Decisions (ADRs Summary)

### ADR-001: Modular Monolith First
- **Decision**: Start as a single deployable unit with clearly bounded modules
- **Rationale**: Prevents premature distributed systems complexity; easier to refactor domain boundaries before splitting
- **Trigger to split**: When a module has independent scaling needs OR independent deployment cadence

### ADR-002: HTMX + Alpine.js for Frontend
- **Decision**: Server-rendered HTML with progressive enhancement
- **Rationale**: Maximizes BE skill focus; HTMX interactions are simpler than SPA for CRUD-heavy marketplace flows; avoids API versioning overhead in Phase 1
- **Tradeoff**: Limited interactivity for complex UX (cart animations, real-time updates)

### ADR-003: PostgreSQL as Primary Datastore
- **Decision**: Single PostgreSQL 16 instance per service (or schema-per-module in monolith)
- **Rationale**: ACID guarantees for financial data (orders, payments, payouts); rich JSON support for variant attributes; mature .NET EF Core support

### ADR-004: Redis for Ephemeral State
- **Decision**: Redis for session data, cart reservation TTLs, refresh token blacklisting, rate-limit counters
- **Rationale**: TTL-native, sub-ms latency, pub/sub capability for future real-time features

### ADR-005: RabbitMQ for Async Messaging (Phase 3+)
- **Decision**: RabbitMQ with MassTransit abstraction layer
- **Rationale**: MassTransit provides saga, outbox pattern, and retry policies; RabbitMQ is operationally simpler than Kafka for low-throughput marketplace events

### ADR-006: YARP as API Gateway (Phase 3+)
- **Decision**: YARP (Yet Another Reverse Proxy) — native .NET solution
- **Rationale**: Zero additional language/runtime; deep integration with ASP.NET middleware; supports JWT pass-through, rate limiting, circuit breaking

---

## 4. Solution Structure

Solution has **14 projects** (11 source + 3 test), managed in `MarketNest.slnx`.

### Root Files

| File | Purpose |
|------|---------|
| `MarketNest.slnx` | .NET solution file (XML format) — all 14 projects |
| `global.json` | Pin .NET SDK version 10.0 |
| `Directory.Build.props` | Global MSBuild: target `net10.0`, nullable, TreatWarningsAsErrors |
| `Directory.Packages.props` | Central Package Management — all NuGet versions in one place |
| `.editorconfig` | Code style: indent, naming (`_camelCase` private fields) |

### Module Internal Architecture (Clean Architecture)

Each module follows 3 layers:

```
Module/
├── Domain/          ← Entities, Aggregates, Value Objects, Domain Events, Enums
├── Application/     ← CQRS Commands, Queries, Validators, DTOs
│   ├── Commands/    ← Each use case = 1 subfolder
│   ├── Queries/     ← Each screen/data need = 1 subfolder
│   └── Validators/  ← FluentValidation paired with each Command
└── Infrastructure/  ← EF Core, Redis, external services
    ├── Persistence/ ← DbContext configurations, migrations
    ├── Repositories/
    ├── Services/
    └── Seeders/     ← IDataSeeder implementations
```

### MarketNest.Core — Shared Kernel

Foundation for the entire solution. All modules reference this project.

```
src/MarketNest.Core/
├── Common/
│   ├── Entity.cs              ← Base entity with Id + DomainEvents list
│   ├── AggregateRoot.cs       ← Aggregate root (extends Entity<Guid>)
│   ├── ValueObject.cs         ← Value object base with structural equality
│   ├── Error.cs               ← Error record (Code, Message, ErrorType)
│   ├── Result.cs              ← Result<T, Error> monad
│   ├── IDataSeeder.cs         ← Database seeding contract (Order, RunInProduction)
│   ├── Cqrs/                  ← ICommand, ICommandHandler, IQuery, IQueryHandler
│   ├── Events/                ← IDomainEvent, IDomainEventHandler
│   ├── Persistence/           ← IBaseRepository<T, TKey>
│   ├── Queries/               ← PagedQuery, PagedResult<T>
│   └── Validation/            ← ValidatorExtensions (MustBeSlug, MustBePositiveMoney...)
├── Contracts/                 ← Cross-module service interfaces
│   ├── IOrderCreationService.cs
│   ├── IInventoryService.cs
│   ├── IPaymentService.cs
│   ├── INotificationService.cs
│   └── IStorefrontReadService.cs
├── Exceptions/                ← DomainException, NotFoundException
└── ValueObjects/              ← Money, Address
```

### MarketNest.Web — ASP.NET Core Host (Composition Root)

```
src/MarketNest.Web/
├── Program.cs                 ← DI registration, middleware, Serilog
├── package.json               ← Tailwind CSS 4 build
├── appsettings.json
├── Infrastructure/            ← DI extensions, middleware, filters
├── Pages/
│   ├── Shared/                ← Layouts + reusable component partials
│   │   ├── _Layout.cshtml     ← Master (topnav, footer, Alpine/HTMX init)
│   │   ├── _LayoutSeller.cshtml ← Seller dashboard (sidebar nav)
│   │   ├── _LayoutAdmin.cshtml  ← Admin layout
│   │   ├── Data/              ← _DataTable, _FilterBar, _SortHeader
│   │   ├── Display/           ← _StatusBadge, _StarRating, _Alert...
│   │   ├── Domain/            ← _ProductCard, _OrderStatusBadge...
│   │   ├── Forms/             ← _TextField, _SelectField, _ImageUpload...
│   │   ├── Navigation/        ← _Pagination, _Tabs, _SidebarNav
│   │   └── Overlays/          ← _Modal, _ConfirmDialog, _Toast...
│   ├── Auth/                  ← Login, Register, Forgot Password
│   ├── Account/               ← Buyer dashboard (Orders, Disputes, Settings)
│   ├── Shop/                  ← Storefront index, Product detail
│   ├── Search/                ← Search results
│   ├── Cart/                  ← Shopping cart
│   ├── Checkout/              ← Checkout flow
│   ├── Orders/                ← Order confirmation
│   ├── Seller/                ← Seller dashboard (Products, Orders, Payouts...)
│   └── Admin/                 ← Admin panel (Users, Disputes, Config...)
└── wwwroot/                   ← CSS, JS, images
```

**Frontend stack**: Razor Pages (SSR) + HTMX 2 + Alpine.js 3 + Tailwind CSS 4

---

## 5. Module Boundaries

Each module maps to a future microservice candidate. **No module crosses another's database table directly.**

| Module | Schema DB | Description | Key Use Cases |
|--------|-----------|-------------|---------------|
| **MarketNest.Identity** | `identity.*` | Users, Roles, JWT, Refresh Tokens, Addresses, Preferences, Privacy, Notification Settings | Register, Login, RefreshToken, UpdateProfile, ManageAddresses, UpdatePreferences |
| **MarketNest.Catalog** | `catalog.*` | Storefronts, Products, Variants, Inventory, Favorite Sellers | CreateProduct, GetProductList, FollowSeller |
| **MarketNest.Cart** | (Redis + `cart.*`) | Shopping Cart, Reservations (TTL 15min), Wishlist | AddToCart, CheckoutCart, AddToWishlist |
| **MarketNest.Orders** | `orders.*` | Orders, Fulfillment, Shipment state machine, Shipping Preferences, Order Preferences | PlaceOrder, ShipOrder, UpdateShippingPreferences |
| **MarketNest.Payments** | `payments.*` | Payments, Payouts, Commission, Payment Methods (Phase 2+) | CapturePayment, ProcessPayout |
| **MarketNest.Reviews** | `reviews.*` | Reviews, Votes, fraud gate | CreateReview, VoteReview |
| **MarketNest.Disputes** | `disputes.*` | Disputes, Messages, Resolution | OpenDispute, ResolveDispute |
| **MarketNest.Notifications** | — | Email/SMS (in-process Phase 1, standalone Phase 3) | SendEmail |
| **MarketNest.Admin** | — | Back-office: arbitration, platform config | Admin commands/queries |

**Communication rules:**
- ❌ Module A CANNOT reference concrete class of Module B
- ✅ Sync: via interfaces in `MarketNest.Core/Contracts/`
- ✅ Async: via domain events (MediatR Phase 1 → RabbitMQ Phase 3)

---

## 6. Data Architecture

### Schema-per-Module Strategy (Monolith)
```sql
CREATE SCHEMA identity;    -- Users, Roles, RefreshTokens, Addresses, Preferences, Privacy, NotificationPrefs
CREATE SCHEMA catalog;     -- Storefronts, Products, Inventory, FavoriteSellers
CREATE SCHEMA cart;        -- WishlistItems (Cart itself is Redis-backed)
CREATE SCHEMA orders;      -- Orders, OrderLines, Fulfillments, ShippingPreferences, OrderPreferences
CREATE SCHEMA payments;    -- Payments, Payouts, Commissions, PaymentMethods (Phase 2+)
CREATE SCHEMA reviews;     -- Reviews, Votes
CREATE SCHEMA disputes;    -- Disputes, Messages, Resolutions
```

### Redis Key Namespaces
```
marketnest:cart:{userId}:reservation:{productVariantId}   TTL: 15min
marketnest:session:{sessionId}                            TTL: 24h
marketnest:ratelimit:{userId}:{endpoint}                  TTL: 1min
marketnest:refresh:{tokenId}                              TTL: 7d
marketnest:blacklist:{tokenId}                            TTL: 7d
```

---

## 7. Infrastructure Architecture

### Docker Compose (Phase 1–2)

| Service | Image | Port | Description |
|---------|-------|------|-------------|
| **app** | Build from Dockerfile | `5000:8080` | .NET monolith |
| **postgres** | `postgres:16-alpine` | `5432:5432` | Primary DB |
| **redis** | `redis:7-alpine` | `6379:6379` | Cache + reservations |
| **rabbitmq** | `rabbitmq:3-management-alpine` | `5672`, `15672` | Message broker |
| **mailhog** | `mailhog/mailhog` | `1025`, `8025` | Fake SMTP (dev) |
| **seq** | `datalust/seq:latest` | `5341:80` | Structured log viewer |

### Kubernetes Topology (Phase 4)
- **Namespaces**: `marketnest-prod`, `marketnest-staging`, `infra`
- **StatefulSets**: PostgreSQL, Redis, RabbitMQ
- **Deployments**: All app services (HPA enabled)
- **Ingress**: Nginx Ingress Controller → YARP → Services
- **GitOps**: ArgoCD watching `infra/k8s/`

### Dockerfile — Multi-stage Build

| Stage | Base Image | Role |
|-------|-----------|------|
| **build** | `mcr.microsoft.com/dotnet/sdk:10.0` | Restore + publish .NET app |
| **css-build** | `node:22-alpine` | Build Tailwind CSS (minified) |
| **runtime** | `mcr.microsoft.com/dotnet/aspnet:10.0` | Final: non-root user, health check |

---

## 8. Observability Stack

| Concern | Tool | Phase |
|---------|------|-------|
| Structured Logging | Serilog → Seq | Phase 2 |
| Distributed Tracing | OpenTelemetry → Seq / Jaeger | Phase 2 |
| Metrics | OpenTelemetry → Prometheus + Grafana | Phase 4 |
| Error Tracking | Sentry (self-hosted optional) | Phase 2 |
| Uptime | Docker healthchecks → K8s liveness probes | Phase 1 / 4 |

---

## 9. Security Architecture

**Defense layers:**
1. Network: HTTPS-only, HSTS, TLS 1.3
2. Auth: JWT (short-lived) + Refresh Token (Redis-backed, revocable)
3. Authorization: RBAC via `IAuthorizationHandler` + Policy-based
4. Input: EF Core parameterized queries, Razor auto-escaping, CSP headers
5. Rate limiting: ASP.NET Core built-in `RateLimiter` middleware
6. Secrets: User Secrets (dev) → Azure Key Vault / Vault (prod)

---

## 10. Testing Strategy

| Layer | Tool | Coverage Target |
|-------|------|-----------------|
| Unit tests | xUnit + FluentAssertions + NSubstitute | Domain logic: 80%+ |
| Integration tests | Testcontainers + WebApplicationFactory | APIs + DB: key paths |
| Architecture tests | NetArchTest | Layer enforcement |
| Contract tests | (Phase 3+) Pact.io | Service boundaries |
| Load testing | k6 | Phase 2 baseline |
| E2E tests | Playwright | Critical user flows |

```
tests/
├── MarketNest.UnitTests/          ← Domain logic + application handlers
├── MarketNest.IntegrationTests/   ← Full stack (Testcontainers, Respawn)
└── MarketNest.ArchitectureTests/  ← NetArchTest layer rules
```

---

## 11. Dependency Graph

```
MarketNest.Web (host)
├── MarketNest.Core (shared kernel)
├── MarketNest.Identity     → Core
├── MarketNest.Catalog      → Core
├── MarketNest.Cart         → Core
├── MarketNest.Orders       → Core
├── MarketNest.Payments     → Core
├── MarketNest.Reviews      → Core
├── MarketNest.Disputes     → Core
├── MarketNest.Notifications → Core
└── MarketNest.Admin        → Core

MarketNest.UnitTests        → Core + all domain modules
MarketNest.IntegrationTests → Web (full stack)
MarketNest.ArchitectureTests → All projects
```

**Rule**: Modules NEVER reference each other. Only reference `Core`. Web references all (composition root).

---

## 12. Quick Start Commands

```bash
# Backend
dotnet restore
dotnet build
dotnet run --project src/MarketNest.Web

# Frontend (Tailwind hot reload)
cd src/MarketNest.Web && npm install && npm run watch:css

# Infrastructure
docker compose up -d

# Tests
dotnet test                                              # All tests
dotnet test tests/MarketNest.UnitTests                   # Unit only
dotnet test tests/MarketNest.ArchitectureTests           # Architecture only
dotnet test tests/MarketNest.IntegrationTests            # Integration (needs Docker)
```

---

## 13. Open Questions / Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Boundary wrong in monolith → painful microservice split | Medium | Strict no-cross-schema rule + event-driven from day 1 |
| HTMX limitation for real-time features | Low | Alpine.js + SSE/WebSocket fallback |
| Redis single point of failure for cart reservations | Low | Acceptable for toy; Redis Sentinel in Phase 4 |
| RabbitMQ message loss on restart | Medium | Enable persistence + quorum queues from Phase 3 |
| Solo developer burnout | High | Phase gates — ship Phase 1 before starting Phase 2 |

---

## Notes

- **NuGet Audit**: `TreatWarningsAsErrors=true` in `Directory.Build.props` will fail restore if packages have known vulnerabilities. Add `<NuGetAudit>false</NuGetAudit>` to temporarily bypass.
- **Program.cs**: Module DI registrations (`AddIdentityModule`, `AddCatalogModule`...) are commented — uncomment as each module is implemented.
- **DatabaseInitializer**: Migration + seeding code is commented — enable when EF Core DbContext exists.

