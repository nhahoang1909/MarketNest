# MarketNest — Project Structure Guide

> Auto-generated: 2026-04-24 | Phase: 1 (Modular Monolith)

---

## 1. Tổng Quan

MarketNest là một **multi-vendor marketplace** (kiểu Etsy/Shopee mini) được thiết kế theo kiến trúc **Modular Monolith** trong Phase 1, với khả năng tách thành Microservices ở Phase 3+.

Solution gồm **14 projects** (11 source + 3 test), tất cả được quản lý trong file `MarketNest.slnx`.

---

## 2. Root Files — Build & Config

| File | Mục đích |
|------|----------|
| `MarketNest.slnx` | .NET solution file (XML format mới) — chứa tất cả 14 projects, chia thành folder `src/` và `tests/` |
| `global.json` | Pin .NET SDK version 10.0, đảm bảo mọi developer dùng cùng version |
| `Directory.Build.props` | Global MSBuild properties cho toàn bộ solution: target `net10.0`, enable nullable, TreatWarningsAsErrors, code style enforcement |
| `Directory.Packages.props` | **Central Package Management** — quản lý version tất cả NuGet packages tại một chỗ duy nhất (MediatR 12, EF Core 10, FluentValidation 11, Serilog 4, xUnit, FluentAssertions, Testcontainers, NetArchTest...) |
| `.editorconfig` | Code style rules: indent, naming conventions (`_camelCase` cho private fields), C# preferences |
| `.gitignore` | Ignore bin/obj, node_modules, secrets (.env), Docker certs, test results |

---

## 3. Backend — `src/` Folder

### 3.1 Kiến Trúc Tổng Quan

Mỗi module tuân theo **Clean Architecture** với 3 layer:

```
Module/
├── Domain/          ← Entities, Aggregates, Value Objects, Domain Events, Enums
│   ├── Entities/
│   ├── Events/
│   ├── Enums/
│   └── ValueObjects/
├── Application/     ← CQRS Commands, Queries, Validators, DTOs
│   ├── Commands/    ← Mỗi use case = 1 subfolder (VD: PlaceOrder/)
│   ├── Queries/     ← Mỗi screen/data need = 1 subfolder
│   └── Validators/  ← FluentValidation paired với mỗi Command
└── Infrastructure/  ← EF Core, Redis, external services
    ├── Persistence/ ← DbContext configurations, migrations
    ├── Repositories/
    ├── Services/
    └── Seeders/     ← IDataSeeder implementations
```

### 3.2 MarketNest.Core — Shared Kernel

**Vai trò**: Foundation chung cho toàn bộ solution. Mọi module đều reference project này.

```
src/MarketNest.Core/
├── Common/
│   ├── Entity.cs              ← Base entity với Id + DomainEvents list
│   ├── AggregateRoot.cs       ← Aggregate root (extends Entity<Guid>)
│   ├── ValueObject.cs         ← Value object base với structural equality
│   ├── Error.cs               ← Error record (Code, Message, ErrorType)
│   ├── Result.cs              ← Result<T, Error> monad — không throw exception cho business failures
│   ├── IDataSeeder.cs         ← Contract cho database seeding (Order, RunInProduction)
│   ├── Cqrs/
│   │   ├── ICommand.cs        ← Marker interface: ICommand<TResult> : IRequest<Result<T, Error>>
│   │   ├── ICommandHandler.cs ← Handler interface cho commands
│   │   ├── IQuery.cs          ← Marker interface: IQuery<TResult> : IRequest<TResult>
│   │   └── IQueryHandler.cs   ← Handler interface cho queries
│   ├── Events/
│   │   ├── IDomainEvent.cs    ← Domain event marker: INotification + EventId + OccurredAt
│   │   └── IDomainEventHandler.cs ← INotificationHandler<TEvent>
│   ├── Persistence/
│   │   └── IBaseRepository.cs ← Generic repository: GetByKey, Add, Update, Remove, SaveChanges
│   ├── Queries/
│   │   ├── PagedQuery.cs      ← Base record cho list screens: Page, PageSize, SortBy, Search
│   │   └── PagedResult.cs     ← Envelope: Items, TotalCount, TotalPages, HasPrev/HasNext
│   └── Validation/
│       └── ValidatorExtensions.cs ← Reusable rules: MustBeSlug, MustBePositiveMoney, MustBeValidId...
├── Contracts/                 ← Cross-module service interfaces (module boundary contracts)
│   ├── IOrderCreationService.cs   ← Cart → Orders: tạo order từ cart snapshot
│   ├── IInventoryService.cs       ← Cart/Orders → Catalog: check/reserve/release stock
│   ├── IPaymentService.cs         ← Orders → Payments: capture/refund
│   ├── INotificationService.cs    ← All → Notifications: send email/SMS
│   └── IStorefrontReadService.cs  ← Payments → Catalog: lấy commission rate
├── Exceptions/
│   ├── DomainException.cs     ← Invariant violations (không phải user input errors)
│   └── NotFoundException.cs   ← Entity not found
└── ValueObjects/
    ├── Money.cs               ← Amount + Currency (immutable)
    └── Address.cs             ← Street, City, State, PostalCode, Country
```

**NuGet packages**: MediatR, FluentValidation

### 3.3 Business Modules

Mỗi module map 1:1 với một bounded context, tương lai có thể tách thành microservice riêng.

| Module | Schema DB | Mô tả | Use Cases (Commands/Queries) |
|--------|-----------|--------|------------------------------|
| **MarketNest.Identity** | `identity.*` | Users, Roles, JWT, Refresh Tokens | Register, Login, RefreshToken, ChangePassword, GetUserProfile |
| **MarketNest.Catalog** | `catalog.*` | Storefronts, Products, Variants, Inventory | CreateProduct, UpdateProduct, CreateStorefront, GetProductList, GetProductDetail, GetStorefrontBySlug |
| **MarketNest.Cart** | (Redis) | Shopping Cart, Cart Items, Reservations (TTL 15min) | AddToCart, RemoveFromCart, CheckoutCart, GetCart |
| **MarketNest.Orders** | `orders.*` | Orders, Order Lines, Fulfillment, Shipment state machine | PlaceOrder, ConfirmOrder, ShipOrder, CancelOrder, GetOrderDetail, GetOrderList |
| **MarketNest.Payments** | `payments.*` | Payments, Payouts, Commission calculation | CapturePayment, ProcessRefund, ProcessPayout, GetPaymentDetail, GetPayoutList |
| **MarketNest.Reviews** | `reviews.*` | Reviews, Review Votes, fraud gate | CreateReview, VoteReview, GetProductReviews |
| **MarketNest.Disputes** | `disputes.*` | Disputes, Messages, Resolution | OpenDispute, AddDisputeMessage, ResolveDispute, GetDisputeDetail, GetDisputeList |
| **MarketNest.Notifications** | — | Email/SMS dispatch (in-process Phase 1, standalone Phase 3) | SendEmail, SendTemplatedEmail |
| **MarketNest.Admin** | — | Back-office: arbitration, platform config | Admin commands/queries |

**Mỗi module có**:
- `.csproj` — reference MarketNest.Core + NuGet packages cần thiết (MediatR, FluentValidation, EF Core, Redis...)
- `AssemblyReference.cs` — marker class cho MediatR/FluentValidation assembly scanning
- Folder structure: `Domain/`, `Application/`, `Infrastructure/`

**Quy tắc module boundary**:
- ❌ Module A KHÔNG được reference concrete class của Module B
- ✅ Giao tiếp sync: qua interface trong `MarketNest.Core/Contracts/`
- ✅ Giao tiếp async: qua domain events (MediatR IPublisher Phase 1 → RabbitMQ Phase 3)

### 3.4 MarketNest.Web — ASP.NET Core Host

**Vai trò**: Composition root — nơi kết nối tất cả modules, middleware pipeline, Razor Pages.

```
src/MarketNest.Web/
├── Program.cs                 ← Composition root: DI registration, middleware pipeline, Serilog
├── package.json               ← Node.js tooling: Tailwind CSS 4 build (npm run watch:css)
├── appsettings.json           ← Configuration (connection strings, Seq URL, JWT settings)
├── appsettings.Development.json
├── Extensions/                ← DI extension methods cho từng module
├── Middleware/                ← Exception handler, correlation ID, rate limiting
├── Pages/
│   ├── Index.cshtml + .cs     ← Home page
│   ├── Error.cshtml + .cs     ← Error page
│   ├── _ViewImports.cshtml    ← Global Razor imports, tag helpers
│   ├── _ViewStart.cshtml      ← Layout assignment
│   ├── Shared/
│   │   ├── _Layout.cshtml     ← Main layout: HTMX 2 + Alpine.js 3 CDN, Tailwind CSS, header/footer
│   │   └── Components/        ← Reusable partials (_ProductCard, _StarRating, _Pagination...)
│   ├── Auth/                  ← Login, Register, Forgot Password
│   ├── Account/               ← Buyer dashboard
│   │   ├── Orders/
│   │   ├── Disputes/
│   │   └── Settings/
│   ├── Seller/                ← Seller dashboard
│   │   ├── Dashboard/
│   │   ├── Storefront/
│   │   ├── Products/
│   │   ├── Orders/
│   │   ├── Payouts/
│   │   ├── Reviews/
│   │   └── Disputes/
│   └── Admin/                 ← Admin panel
│       ├── Dashboard/
│       ├── Users/
│       ├── Storefronts/
│       ├── Disputes/
│       ├── Config/
│       └── Notifications/
└── wwwroot/
    ├── css/
    │   └── input.css          ← Tailwind CSS entry point (brand theme: orange #f97316)
    ├── js/
    ├── images/
    └── lib/
```

**Program.cs bao gồm**:
- Serilog bootstrap + structured logging → Seq
- MediatR registration (scan tất cả module assemblies)
- FluentValidation auto-discovery
- Middleware pipeline: HTTPS → Static Files → Serilog → Routing → Auth → Antiforgery → Razor Pages
- Health check endpoint: `GET /health`
- Partial class `Program` cho WebApplicationFactory (integration tests)

**Frontend stack**: Razor Pages (SSR) + HTMX 2 (partial updates) + Alpine.js 3 (client reactivity) + Tailwind CSS 4

**NuGet packages**: Tất cả module references + Npgsql (PostgreSQL), Serilog, OpenTelemetry, JWT Bearer, EF Core Design

---

## 4. Tests — `tests/` Folder

| Project | Mục đích | Packages |
|---------|----------|----------|
| **MarketNest.UnitTests** | Test domain logic + application handlers, không cần database/container | xUnit, FluentAssertions, NSubstitute |
| **MarketNest.IntegrationTests** | Full stack tests: real PostgreSQL + Redis via Testcontainers, WebApplicationFactory | xUnit, FluentAssertions, Testcontainers, Respawn, Mvc.Testing |
| **MarketNest.ArchitectureTests** | Enforce layer rules: Domain không reference EF Core, Web không gọi Repository trực tiếp... | xUnit, FluentAssertions, NetArchTest |

```
tests/
├── MarketNest.UnitTests/
│   ├── Domain/           ← Test aggregates, value objects, domain methods
│   └── Application/      ← Test command/query handlers (mocked dependencies)
├── MarketNest.IntegrationTests/
│   ├── Fixtures/         ← WebApplicationFactory, Testcontainers setup
│   └── Modules/          ← Integration tests theo module
└── MarketNest.ArchitectureTests/
    └── (NetArchTest rules)
```

---

## 5. Infrastructure — `infra/` Folder

```
infra/
├── nginx/
│   ├── nginx.conf         ← Reverse proxy config: SSL/TLS 1.3, HSTS, rate limiting, proxy to app:8080
│   ├── conf.d/            ← Additional Nginx configs (empty, ready for custom rules)
│   └── certs/             ← SSL certificates (gitignored)
└── k8s/                   ← Phase 4: Kubernetes manifests
    ├── base/
    │   ├── core-service/       ← Deployment, Service, HPA, ConfigMap cho main app
    │   ├── notification-service/ ← Phase 3: extracted notification service
    │   ├── postgres/           ← StatefulSet cho PostgreSQL
    │   ├── redis/              ← StatefulSet cho Redis
    │   └── rabbitmq/           ← StatefulSet cho RabbitMQ
    ├── overlays/
    │   ├── staging/            ← Kustomize overlay cho staging
    │   └── production/         ← Kustomize overlay cho production
    ├── ingress/
    │   └── (nginx-ingress.yaml)
    └── helm/
        └── marketnest/         ← Helm chart (alternative to Kustomize)
```

**`nginx.conf` cấu hình**:
- HTTP → HTTPS redirect (301)
- TLS 1.3 only
- Security headers: HSTS, X-Frame-Options DENY, X-Content-Type-Options nosniff
- Gzip compression
- Rate limiting: `api` zone (30 req/s), `auth` zone (5 req/min — brute force protection)
- Reverse proxy đến `app:8080`

---

## 6. Docker & Containerization

### 6.1 `Dockerfile` — Multi-stage Build

| Stage | Base Image | Vai trò |
|-------|-----------|---------|
| **build** | `mcr.microsoft.com/dotnet/sdk:10.0` | Restore + publish .NET app |
| **css-build** | `node:22-alpine` | Build Tailwind CSS (minified) |
| **runtime** | `mcr.microsoft.com/dotnet/aspnet:10.0` | Final image: chạy app, non-root user, health check |

- Expose port `8080`
- Health check: `curl -f http://localhost:8080/health`
- Non-root user `appuser` cho security
- Layer caching: copy `.csproj` files trước → restore → copy source → publish

### 6.2 `docker-compose.yml` — Development Environment

| Service | Image | Port | Mô tả |
|---------|-------|------|--------|
| **app** | Build từ Dockerfile | `5000:8080` | .NET monolith |
| **postgres** | `postgres:16-alpine` | `5432:5432` | Database chính. User: `mn`, Password: `mn_secret`, DB: `marketnest` |
| **redis** | `redis:7-alpine` | `6379:6379` | Cache + cart reservations + refresh token blacklist |
| **rabbitmq** | `rabbitmq:3-management-alpine` | `5672`, `15672` | Message broker (ready cho Phase 3). Management UI: http://localhost:15672 |
| **mailhog** | `mailhog/mailhog` | `1025`, `8025` | Fake SMTP server. Web UI xem email: http://localhost:8025 |
| **seq** | `datalust/seq:latest` | `5341:80` | Structured log viewer. UI: http://localhost:5341 |

- PostgreSQL có health check (`pg_isready`)
- Redis có AOF persistence
- Volumes: `pg_data`, `redis_data`, `seq_data`

### 6.3 `docker-compose.prod.yml` — Production Overrides

- Thêm **Nginx** reverse proxy (port 80/443)
- App dùng pre-built image từ GHCR (`ghcr.io/youruser/marketnest:${TAG}`)
- Memory limit: 512MB
- Ẩn ports internal services (Postgres, Redis, RabbitMQ)
- Loại bỏ MailHog
- `restart: unless-stopped` cho reliability

---

## 7. CI/CD — `.github/workflows/`

### 7.1 `ci.yml` — Continuous Integration

**Trigger**: Push to `main`/`develop`, Pull Request to `main`

**Pipeline**:
1. Spin up PostgreSQL 16 + Redis 7 services
2. Setup .NET 10 SDK
3. Cache NuGet packages
4. `dotnet restore` → `dotnet build` (Release)
5. Run **Unit Tests**
6. Run **Architecture Tests** (NetArchTest)
7. Run **Integration Tests** (với real DB/Redis)
8. Nếu push to `main`: Build Docker image → Push to GitHub Container Registry (GHCR)

### 7.2 `deploy.yml` — Deployment

**Trigger**: Tag push (`v*`, ví dụ `v1.0.0`)

**Pipeline**: SSH vào production server → `docker compose pull` → `docker compose up -d` → prune old images

---

## 8. Frontend Tooling

| File | Mô tả |
|------|--------|
| `src/MarketNest.Web/package.json` | npm scripts: `build:css` (minified), `watch:css` (dev hot reload) |
| `src/MarketNest.Web/wwwroot/css/input.css` | Tailwind CSS 4 entry: brand theme (orange `#f97316`), surface colors, Inter font |

**Không cần bundler** (Webpack/Vite) — HTMX + Alpine.js loaded từ CDN trực tiếp trong `_Layout.cshtml`.

---

## 9. Dependency Graph

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

MarketNest.UnitTests        → Core, Identity, Catalog, Cart, Orders, Payments, Reviews, Disputes
MarketNest.IntegrationTests → Web (full stack)
MarketNest.ArchitectureTests → All projects (layer rule enforcement)
```

**Quy tắc**: Modules KHÔNG reference lẫn nhau. Chỉ reference `Core`. Web reference tất cả (composition root).

---

## 10. Quick Start Commands

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

## 11. Chú Ý Quan Trọng

- **NuGet Audit**: `TreatWarningsAsErrors=true` trong `Directory.Build.props` sẽ fail restore nếu NuGet packages có known vulnerabilities. Thêm `<NuGetAudit>false</NuGetAudit>` để tạm bypass, hoặc upgrade packages.
- **Program.cs**: Các module DI registration (`AddIdentityModule`, `AddCatalogModule`...) đang commented — sẽ uncomment khi implement từng module.
- **DatabaseInitializer**: Code migration + seeding đang commented — sẽ enable khi có EF Core DbContext.
- **Folders có `{braces}`**: Một số query folders được tạo với `{}` (ví dụ `{GetCart}`) — nên rename bỏ braces.
