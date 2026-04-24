# MarketNest — Architecture Requirements

> Version: 0.1 (Planning) | Status: Draft | Date: 2026-04

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
- **Tradeoff**: Not suitable for analytics at scale (acceptable for toy project)

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

## 4. Module Boundaries (Monolith Phase)

Each module maps to a future microservice candidate. **No module crosses another's database table directly** — only via domain events or explicit service interfaces.

```
src/
├── MarketNest.Core/            ← Shared kernel (value objects, base entities)
├── MarketNest.Identity/        ← Auth: Users, Roles, JWT, Refresh Tokens
├── MarketNest.Catalog/         ← Storefront, Product, ProductVariant, Inventory
├── MarketNest.Cart/            ← Cart, CartItem, Redis TTL reservations
├── MarketNest.Orders/          ← Order, OrderLine, Fulfillment, Shipment
├── MarketNest.Payments/        ← Payment, Payout, Commission calculation
├── MarketNest.Reviews/         ← Review, ReviewVote, fraud gate
├── MarketNest.Disputes/        ← Dispute, DisputeMessage, Resolution
├── MarketNest.Notifications/   ← Email/SMS dispatch (split out in Phase 3)
└── MarketNest.Admin/           ← Back-office: arbitration, platform config
```

**Communication rules in monolith:**
- Synchronous: Direct method call via interfaces (no HTTP between modules)
- Asynchronous: In-process `IPublisher` (MediatR events) → externalize to RabbitMQ in Phase 3

---

## 5. Data Architecture

### Schema-per-Module Strategy (Monolith)
```sql
-- Each module owns its schema
CREATE SCHEMA identity;    -- Users, Roles, RefreshTokens
CREATE SCHEMA catalog;     -- Storefronts, Products, Inventory
CREATE SCHEMA orders;      -- Orders, OrderLines, Fulfillments
CREATE SCHEMA payments;    -- Payments, Payouts, Commissions
CREATE SCHEMA reviews;     -- Reviews, Votes
CREATE SCHEMA disputes;    -- Disputes, Messages, Resolutions
```

This enables future database-per-service extraction with minimal changes.

### Redis Key Namespaces
```
marketnest:cart:{userId}:reservation:{productVariantId}   TTL: 15min
marketnest:session:{sessionId}                            TTL: 24h
marketnest:ratelimit:{userId}:{endpoint}                  TTL: 1min
marketnest:refresh:{tokenId}                              TTL: 7d
marketnest:blacklist:{tokenId}                            TTL: 7d
```

---

## 6. Infrastructure Architecture

### Phase 1–2: Docker Compose
```yaml
services:
  app:          # .NET 10 monolith
  postgres:     # PostgreSQL 16
  redis:        # Redis 7
  rabbitmq:     # RabbitMQ 3.x (ready for Phase 3)
  seq:          # Structured log viewer
  nginx:        # Reverse proxy + SSL termination
  mailhog:      # Local mail server (dev)
```

### Phase 4: Kubernetes Topology
- **Namespaces**: `marketnest-prod`, `marketnest-staging`, `infra`
- **StatefulSets**: PostgreSQL, Redis, RabbitMQ
- **Deployments**: All application services (HPA enabled)
- **Ingress**: Nginx Ingress Controller → YARP → Services
- **GitOps**: ArgoCD watching `infra/k8s/` directory

---

## 7. Observability Stack

| Concern | Tool | Phase |
|---------|------|-------|
| Structured Logging | Serilog → Seq | Phase 2 |
| Distributed Tracing | OpenTelemetry → Seq / Jaeger | Phase 2 |
| Metrics | OpenTelemetry → Prometheus + Grafana | Phase 4 |
| Error Tracking | Sentry (self-hosted optional) | Phase 2 |
| Uptime | Docker healthchecks → K8s liveness probes | Phase 1 / 4 |

---

## 8. Security Architecture

See `security-requirements.md` (embedded in backend-requirements.md).

**Defense layers:**
1. Network: HTTPS-only, HSTS, TLS 1.3
2. Auth: JWT (short-lived) + Refresh Token (Redis-backed, revocable)
3. Authorization: RBAC via `IAuthorizationHandler` + Policy-based
4. Input: EF Core parameterized queries, Razor auto-escaping, CSP headers
5. Rate limiting: ASP.NET Core built-in `RateLimiter` middleware
6. Secrets: User Secrets (dev) → Azure Key Vault / Vault (prod)

---

## 9. Testing Strategy

| Layer | Tool | Coverage Target |
|-------|------|-----------------|
| Unit tests | xUnit + FluentAssertions | Domain logic: 80%+ |
| Integration tests | Testcontainers + WebApplicationFactory | APIs + DB: key paths |
| Contract tests | (Phase 3+) Pact.io | Service boundaries |
| Load testing | k6 | Phase 2 baseline |
| E2E tests | Playwright | Critical user flows |

---

## 10. Open Questions / Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Boundary wrong in monolith → painful microservice split | Medium | Strict no-cross-schema rule + event-driven from day 1 |
| HTMX limitation for real-time features | Low | Alpine.js + SSE/WebSocket fallback acceptable |
| Redis single point of failure for cart reservations | Low | Acceptable for toy; Redis Sentinel in Phase 4 |
| RabbitMQ message loss on restart | Medium | Enable persistence + quorum queues from Phase 3 |
| Solo developer burnout | High | Phase gates — ship Phase 1 before starting Phase 2 |
