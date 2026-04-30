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
14. [Module Layer Patterns](#14-module-layer-patterns)

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

Solution has **18 projects** (14 source + 4 test), managed in `MarketNest.slnx`.

### Root Files

| File | Purpose |
|------|---------|
| `MarketNest.slnx` | .NET solution file (XML format) — all 18 projects |
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

### Base Packages — Shared Kernel

Foundation for the entire solution. All modules reference these `Base.*` packages (the previous `MarketNest.Core` project has been split and removed from the solution).

```
src/Base/
├── MarketNest.Base.Domain/        ← Entity<T>, AggregateRoot, ValueObject, domain event interfaces
│   ├── Entity.cs
│   ├── AggregateRoot.cs
│   ├── ValueObject.cs
│   ├── Events/                    ← IDomainEvent, IPreCommitDomainEvent, IHasDomainEvents
│   ├── ReferenceData/             ← ReferenceData base class
│   └── ValueObjects/              ← (domain-level VOs)
│
├── MarketNest.Base.Common/        ← Application-layer shared contracts, DTOs, constants
│   ├── Error.cs                   ← Error record (Code, Message, ErrorType)
│   ├── Result.cs                  ← Result<T, Error> monad
│   ├── IDataSeeder.cs             ← Database seeding contract (Order, RunInProduction)
│   ├── DomainConstants.cs         ← Pagination defaults, validation limits, error codes
│   ├── TableConstants.cs          ← Schema names, system table names
│   ├── StatusNames.cs             ← OrderStatusNames, EntityStatusNames
│   ├── CacheKeys.cs               ← Redis cache key templates
│   ├── SlaConstants.cs            ← SLA thresholds
│   ├── Attributes/                ← [Transaction], [NoTransaction], [Auditable], [Audited]
│   ├── Contracts/                 ← Cross-module service interfaces
│   │   ├── IAuditService.cs
│   │   ├── ICacheService.cs
│   │   ├── ICurrentUser.cs
│   │   ├── IInventoryService.cs
│   │   ├── INotificationService.cs
│   │   ├── IOrderCreationService.cs
│   │   ├── IPaymentService.cs
│   │   ├── IReferenceDataReadService.cs
│   │   ├── IRuntimeContext.cs
│   │   ├── IStorefrontReadService.cs
│   │   ├── IUserTimeZoneProvider.cs
│   │   └── Config/               ← IXxxConfig + IXxxConfigWriter contracts
│   ├── Cqrs/                      ← ICommand, ICommandHandler, IQuery, IQueryHandler
│   ├── Events/                    ← IIntegrationEvent, IEventBus, IntegrationEvents/
│   ├── Excel/                     ← IExcelService, ExcelTemplate, ExcelUploadRules
│   ├── Exceptions/                ← DomainException, NotFoundException, UnauthorizedException
│   ├── Queries/                   ← PagedQuery, PagedResult<T>, IBaseQuery<T,K>
│   ├── Security/                  ← IAntivirusScanner
│   ├── Sequences/                 ← ISequenceService, SequenceDescriptor
│   ├── Validation/                ← ValidatorExtensions, FieldLimits, ValidationMessages
│   └── ValueObjects/              ← Money, Address
│
├── MarketNest.Base.Infrastructure/ ← Infrastructure base classes
│   ├── Logging/                   ← IAppLogger<T>, LogEventId enum
│   └── Persistence/               ← BaseQuery<T,K,Ctx>, BaseRepository<T,K,Ctx>,
│                                     IBaseRepository<T,K>, IUnitOfWork, IModuleDbContext,
│                                     DddModelBuilderExtensions, PgQueryBuilder
│
├── MarketNest.Base.Api/           ← ReadApiV1ControllerBase, WriteApiV1ControllerBase
│
└── MarketNest.Base.Utility/       ← Slug generation, date extensions
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
| **MarketNest.Notifications** | `notifications.*` | NotificationTemplate (admin-managed), Notification (in-app inbox), Email dispatch (MailKit/SMTP), Handlebars template engine. Phase 3: standalone service via RabbitMQ. | `INotificationService.SendAsync()`, `INotificationService.SendSecurityEmailAsync()` |
| **MarketNest.Promotions** | `promotions.*` | Vouchers (Platform + Shop), VoucherUsages, discount validation | CreateVoucher, ApplyVoucher, ValidateVoucher |
| **MarketNest.Admin** | `admin.*` + `public.*` | Back-office: arbitration, platform config, reference data management | Admin commands/queries, ArbitrateDispute, UpdateCommission |

**Communication rules:**
- ❌ Module A CANNOT reference concrete class of Module B
- ✅ Sync: via interfaces in `MarketNest.Base.Common/Contracts/`
- ✅ Admin writes business config via `IXxxConfigWriter` contracts — never injects module DbContexts
- ✅ Async: via domain events (MediatR Phase 1 → RabbitMQ Phase 3)

### Three-Tier Configuration Model (ADR-021)

| Tier | Examples | Owner | Storage | Access |
|------|----------|-------|---------|--------|
| **Tier 1 — Reference Data** | Country, Gender, ProductCategory | Admin module | `public` schema tables | `IReferenceDataReadService` (Redis 24h TTL) |
| **Tier 2 — Business Config** | CommissionRate, OrderPolicyConfig | Owning module (Orders, Payments, etc.) | Module schema table | `IXxxConfig` read / `IXxxConfigWriter` write contracts |
| **Tier 3 — System Config** | PasswordMinLength, DefaultCurrency | `MarketNest.Web` | `appsettings.json` only | `IOptions<T>` injection |

---

## 6. Data Architecture

### Schema-per-Module Strategy (Monolith)
```sql
CREATE SCHEMA identity;    -- Users, Roles, RefreshTokens, Addresses, Preferences, Privacy, NotificationPrefs
CREATE SCHEMA catalog;     -- Storefronts, Products, Inventory, FavoriteSellers
CREATE SCHEMA cart;        -- WishlistItems (Cart itself is Redis-backed)
CREATE SCHEMA orders;      -- Orders, OrderLines, Fulfillments, ShippingPreferences, OrderPreferences
CREATE SCHEMA payments;    -- Payments, Payouts (Commissions in Payout aggregate), PaymentMethods (Phase 2+)
CREATE SCHEMA reviews;     -- Reviews, Votes
CREATE SCHEMA disputes;    -- Disputes, Messages, Resolutions
CREATE SCHEMA promotions;  -- Vouchers, VoucherUsages
```

### Redis Key Namespaces
```
marketnest:cart:{userId}:reservation:{productVariantId}   TTL: 15min
marketnest:session:{sessionId}                            TTL: 24h
marketnest:ratelimit:{userId}:{endpoint}                  TTL: 1min
marketnest:refresh:{tokenId}                              TTL: 7d
marketnest:blacklist:{tokenId}                            TTL: 7d
marketnest:voucher:validate:{code}                        TTL: 30s  (invalidate on Pause/Deplete/Expire)

# Tier 1 — Reference Data (24h TTL)
marketnest:refdata:countries                              TTL: 24h
marketnest:refdata:genders                                TTL: 24h
marketnest:refdata:phone-codes                            TTL: 24h
marketnest:refdata:nationalities                          TTL: 24h
marketnest:refdata:categories                             TTL: 24h

# Tier 2 — Business Config (1h TTL, invalidated on Admin write)
marketnest:config:order-policy                            TTL: 1h
marketnest:config:commission-default                      TTL: 1h
marketnest:config:commission:seller:{sellerId}            TTL: 1h
marketnest:config:storefront-policy                       TTL: 1h
marketnest:config:review-policy                           TTL: 1h
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
MarketNest.Web (host / composition root)
├── Base.Domain              ← Entities, AggregateRoot, ValueObject
├── Base.Common              ← Result, Error, CQRS, Contracts, Queries, Validation
├── Base.Infrastructure      ← BaseQuery, BaseRepository, IUnitOfWork, Logging
├── Base.Api                 ← Controller base classes
├── Base.Utility             ← Slug, date helpers
├── MarketNest.Analyzers     ← Roslyn analyzers
├── MarketNest.Identity      → Base.*
├── MarketNest.Catalog       → Base.*
├── MarketNest.Cart          → Base.*
├── MarketNest.Orders        → Base.*
├── MarketNest.Payments      → Base.*
├── MarketNest.Reviews       → Base.*
├── MarketNest.Disputes      → Base.*
├── MarketNest.Promotions    → Base.*
├── MarketNest.Notifications → Base.*
└── MarketNest.Admin         → Base.*

MarketNest.UnitTests         → Base.* + all domain modules
MarketNest.IntegrationTests  → Web (full stack)
MarketNest.ArchitectureTests → All projects
```

**Rule**: Modules NEVER reference each other. Only reference `Base.*` packages. Web references all (composition root).

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

---

## 14. Module Layer Patterns

### Read/Write DbContext Split

Each module has two DbContexts:
- `{Module}DbContext` — write context, change tracking ON, implements `IModuleDbContext`, runs migrations
- `{Module}ReadDbContext` — read context, `NoTracking` globally, does NOT implement `IModuleDbContext`

Both contexts use the same `IEntityTypeConfiguration<T>` classes via `ApplyConfigurationsFromAssembly(typeof({Module}DbContext).Assembly)`.

### Query Contracts

- `IBaseQuery<TEntity, TKey>` (Base.Common) — simple reads: GetByKey, Exists, List, FirstOrDefault, Count
- `I{Entity}Query` (Application) — extends IBaseQuery, simple module-specific reads
- `IGet{UseCase}Query` (Application) — complex reads (projections, pagination, joins) get a dedicated interface

Rule: any query involving DTO projection, pagination, or multi-table joins MUST use a dedicated `IGet{UseCase}Query` interface, not a method on `I{Entity}Query`.

### Repository Contracts

- `IBaseRepository<TEntity, TKey>` (Base.Infrastructure) — write operations: Add, Update, Remove, GetByKey
- `I{Entity}Repository` (Application) — extends IBaseRepository, adds aggregate-specific operations

### Abstract Base Classes (Infrastructure/Persistence)

- `BaseRepository<TEntity, TKey>` — default EF Core impl of IBaseRepository using `{Module}DbContext`
- `BaseQuery<TEntity, TKey>` — default EF Core impl of IBaseQuery using `{Module}ReadDbContext`

Concrete classes override only what is module-specific (e.g., loading with `Include`).

### Controller Base Classes (Infrastructure/Api/Common)

- `ApiV1ControllerBase` — shared base: `IMediator`, `MapError(Error)` helper
- `ReadApiV1ControllerBase` — extends base, used by GET-only controllers
- `WriteApiV1ControllerBase` — extends base, used by POST/PUT/DELETE controllers

### Feature Folder Layout (within layers)

```
Application/Submodule/{Feature}/
  Commands/                     # ICommand<T> records
  CommandHandlers/              # ICommandHandler implementations
  QueryHandlers/                # IQueryHandler implementations
  Queries/                      # IQuery<T> records + IBaseQuery interfaces + DTOs
  Repositories/                 # IBaseRepository interfaces
  Validators/                   # FluentValidation validators
  DomainEventHandlers/          # IDomainEventHandler implementations
  IntegrationEventHandlers/     # Integration event handlers

Infrastructure/
  Api/{Feature}/                # ReadController + WriteController
  Queries/{Feature}/            # IBaseQuery implementations
  Repositories/{Feature}/       # IBaseRepository implementations
  Persistence/                  # DbContexts, BaseRepository, BaseQuery, Configurations/
```

---

## Notes

- **NuGet Audit**: `TreatWarningsAsErrors=true` in `Directory.Build.props` will fail restore if packages have known vulnerabilities. Add `<NuGetAudit>false</NuGetAudit>` to temporarily bypass.
- **Program.cs**: Module DI registrations (`AddIdentityModule`, `AddCatalogModule`...) are commented — uncomment as each module is implemented.
- **DatabaseInitializer**: Migration + seeding code is commented — enable when EF Core DbContext exists.

