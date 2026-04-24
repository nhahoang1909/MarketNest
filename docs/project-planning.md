# MarketNest — Project Planning

> Version: 0.1 (Planning) | Status: Draft | Date: 2026-04  
> 6–9 month solo project. Ship phase gates before starting next phase.

---

## 0. Principles for Solo Projects

- **Ship Phase 1 before writing Phase 2 code** — no premature architecture
- **Working > Perfect** — deployed with limitations > undeployed perfection  
- **One task at a time** — no multitasking between modules
- **Timeboxes are real** — if a task exceeds 2x estimate, simplify scope, don't extend indefinitely
- **Test the happy path first**, then edge cases

---

## Phase 1 — Monolith (Month 1–3)

**Exit Criteria**: Deployed to production, at least 1 real user can browse, register, create a storefront, list a product, and place an order.

---

### Month 1, Week 1–2: Foundation

| Task | Est. | Notes |
|------|------|-------|
| Solution setup: folders, projects, solution file | 0.5d | Follow `code-rules.md` structure |
| Shared kernel: Entity, AggregateRoot, ValueObject, Result<T> | 1d | No shortcuts here — used everywhere |
| Docker Compose: Postgres + Redis + MailHog + Seq | 0.5d | Verify all services start healthy |
| EF Core setup: DbContext, migrations runner, base config | 1d | Schema-per-module from day 1 |
| GitHub Actions: CI (build + unit test) | 0.5d | Must go green before any feature work |
| Tailwind build pipeline integration | 0.5d | |
| Layout: `_Layout.cshtml`, Tailwind base, nav structure | 1d | |

### Month 1, Week 3–4: Identity Module

| Task | Est. | Notes |
|------|------|-------|
| ASP.NET Core Identity setup (custom User entity) | 1d | |
| Register + email verification flow | 1d | MailHog for dev email |
| Login + JWT generation (access + refresh) | 1.5d | RS256, Redis refresh token store |
| Logout + token revocation | 0.5d | |
| RBAC: Buyer / Seller / Admin roles | 0.5d | |
| Razor Pages: Login, Register, Forgot Password | 1d | |
| Auth middleware pipeline (HSTS, rate limit /auth/) | 0.5d | |

**Month 1 total: ~10 working days**

---

### Month 2, Week 1–2: Catalog Module

| Task | Est. | Notes |
|------|------|-------|
| Storefront domain + EF config | 1d | Slug, status, seller link |
| Product + ProductVariant + InventoryItem domain | 1.5d | Value objects: Money, Sku, Rating |
| Storefront CQRS: create, activate, get | 1d | |
| Product CQRS: create, publish, archive | 1d | |
| Variant CQRS: add, update price, update stock | 1d | |
| Public pages: Storefront page, Product detail | 1.5d | |
| Seller pages: Dashboard, Product list, Product editor | 2d | Multi-step form for variants |
| Image upload: local wwwroot storage (Phase 1) | 0.5d | |

### Month 2, Week 3–4: Cart Module + Search

| Task | Est. | Notes |
|------|------|-------|
| Cart domain + EF config | 0.5d | |
| Redis reservation service | 1d | Lua script for atomicity |
| Add/Remove/Update cart quantity CQRS | 1d | |
| Cart TTL cleanup background job | 0.5d | |
| Cart Razor Page + HTMX partials | 1d | Reservation timer (Alpine) |
| Guest cart → user cart merge on login | 0.5d | |
| Search/Browse: full-text search (PostgreSQL `tsvector`) | 1.5d | Basic FTS, no Elasticsearch yet |
| Home page, Search results page | 1d | HTMX filter, pagination |

**Month 2 total: ~13 working days**

---

### Month 3, Week 1–2: Orders Module

| Task | Est. | Notes |
|------|------|-------|
| Order domain + EF config | 1.5d | State machine, OrderLine, Fulfillment |
| Checkout flow CQRS: CartToOrder | 1d | Atomic: release reservations + create order |
| Payment stub: always succeeds in Phase 1 | 0.5d | IPaymentGateway interface ready for Phase 2 |
| Order state transitions: Confirm, Ship, Deliver | 1d | |
| Auto-cancel / auto-deliver background jobs | 0.5d | Hangfire setup |
| Buyer: Orders list, Order detail, Confirmation page | 1d | |
| Seller: Orders queue, Order detail, Add tracking | 1d | |
| Email notifications (MailKit + MailHog in dev) | 0.5d | Order placed, shipped |

### Month 3, Week 3–4: Reviews + Disputes + Deploy

| Task | Est. | Notes |
|------|------|-------|
| Review domain (with gate check) | 1d | |
| Review CQRS + page | 0.5d | |
| Dispute domain + messages | 1.5d | |
| Dispute pages: open, respond, admin arbitrate | 1d | |
| Commission + Payout stub (records only, no real money) | 0.5d | |
| Admin pages: Users, Storefronts, Disputes queue | 1d | |
| Nginx config + SSL (Let's Encrypt) | 0.5d | |
| GitHub Actions: deploy to VPS on tag | 0.5d | |
| **DEPLOY TO PRODUCTION** | 1d | |

**Month 3 total: ~12 working days**

---

## Phase 2 — Hardening (Month 4–5)

**Exit Criteria**: Production-grade monolith — observable, tested, secure, performant under realistic load.

---

### Month 4: Observability + Testing

| Task | Est. | Notes |
|------|------|-------|
| Serilog + Seq structured logging | 1d | Correlation ID middleware |
| OpenTelemetry tracing → Seq | 1d | EF Core + Redis instrumentation |
| OpenTelemetry metrics → Prometheus | 0.5d | |
| Grafana dashboards: request rates, DB query p95 | 0.5d | |
| Integration tests: WebApplicationFactory + Testcontainers | 2d | Core flows: checkout, dispute |
| Architecture tests: NetArchTest layer rules | 0.5d | |
| Fix all bugs found from testing | 2d | (buffer) |
| k6 smoke test + baseline performance numbers | 0.5d | |

### Month 5: Security + Performance

| Task | Est. | Notes |
|------|------|-------|
| Security audit: CSP headers, rate limits, OWASP Top 10 review | 1d | |
| Refresh token rotation (detect token theft) | 0.5d | |
| Admin audit log (who changed what, when) | 1d | |
| PostgreSQL: add missing indexes from slow query analysis | 1d | Use EXPLAIN ANALYZE |
| Image optimization: WebP conversion on upload | 0.5d | |
| Nginx caching headers for static assets | 0.5d | |
| Playwright E2E tests: register → buy flow | 1d | |
| Load test with k6: 50 concurrent users | 0.5d | |
| Fix performance regressions | 1d | (buffer) |

---

## Phase 3 — First Microservice Split (Month 6–7)

**Exit Criteria**: Notification Service runs as independent process, communicates via RabbitMQ. Core monolith is unaware of email specifics.

---

### Month 6: Message Bus + Outbox

| Task | Est. | Notes |
|------|------|-------|
| MassTransit configuration + RabbitMQ topology | 1d | Quorum queues, DLQ |
| Outbox pattern: EF Core + MassTransit Outbox | 1.5d | Critical for reliability |
| Replace in-process domain event dispatch with MassTransit | 1.5d | Keep IPublisher abstraction |
| Notification Service: new .NET project | 0.5d | Minimal API host |
| Notification Service: consume domain events, send emails | 1.5d | MailKit, email templates |
| YARP API Gateway setup + routing | 1d | Core monolith + Notification Service |
| Integration tests: event flow end-to-end | 1d | |

### Month 7: Polish + Observability for Distributed

| Task | Est. | Notes |
|------|------|-------|
| Distributed tracing across services (trace ID propagation) | 1d | Via message headers |
| Circuit breaker: Polly for inter-service calls | 0.5d | |
| Health checks: Gateway → each service | 0.5d | |
| RabbitMQ dead letter queue monitoring | 0.5d | |
| API Gateway rate limiting (cross-service) | 0.5d | |
| Documentation: system diagram updated | 0.5d | |
| Chaos testing: kill Notification Service, verify core still works | 0.5d | |

---

## Phase 4 — Kubernetes (Month 8–9)

**Exit Criteria**: Full system running on K8s locally (kind), deployed to cloud cluster with ArgoCD GitOps.

---

### Month 8: Local K8s

| Task | Est. | Notes |
|------|------|-------|
| kind cluster setup, namespace, RBAC basics | 0.5d | |
| Kubernetes manifests: Deployments, Services, ConfigMaps | 2d | Core service + Notification service |
| StatefulSets: PostgreSQL, Redis, RabbitMQ | 1.5d | PersistentVolumeClaims |
| Secrets: K8s secrets + external-secrets-operator planning | 0.5d | |
| Nginx Ingress + TLS cert-manager | 1d | |
| HPA for core service (CPU + memory) | 0.5d | |
| Helm chart: parameterize all manifests | 1.5d | staging vs prod values |

### Month 9: GitOps + Cloud

| Task | Est. | Notes |
|------|------|-------|
| ArgoCD setup + Application definitions | 1d | |
| Kustomize overlays: staging + production | 1d | |
| Cloud cluster provisioning: AKS or EKS | 1d | |
| ArgoCD deployment to cloud cluster | 0.5d | |
| Observability: Prometheus + Grafana in K8s | 1d | |
| Load test on K8s: verify HPA scales correctly | 0.5d | |
| Runbook: how to deploy, rollback, debug | 0.5d | |
| **Phase 4 retrospective + what to build next** | 0.5d | |

---

## Risk Register

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|-----------|
| Phase 1 scope creep — too many features | High | High | Ship admin as read-only first; disputes can be manual Phase 1 |
| Domain boundaries wrong → painful refactor | Medium | High | Architecture tests from day 1; strict no-cross-schema |
| Redis not available → cart broken | Low | Medium | Graceful degradation: skip reservation if Redis down |
| Solo burnout | Medium | High | Phase gates — celebrate each phase ship |
| PostgreSQL migration breaks prod | Medium | High | Always test migrations on staging first |

---

## Velocity Assumptions

- **Working days per week**: 5 (solo, realistic pace)
- **Productive hours per day**: 4–5 (accounting for context switching)
- **Buffer built in**: ~20% per phase for bugs/rework
- **If falling behind**: cut features (e.g., dispute photos → text only), not quality

---

## Definition of Done (per task)

- [ ] Feature works end-to-end in local Docker Compose
- [ ] Unit test covers the domain rule
- [ ] Integration test covers the API path (Phase 2+)
- [ ] No new warnings in architecture test suite
- [ ] No credentials committed to git
- [ ] Code review checklist passed (self-review for solo)

---

## Recommended Learning Resources (Phase-Aligned)

| Phase | Resources |
|-------|-----------|
| Phase 1 | "Domain-Driven Design" (Evans), "Clean Architecture" (Martin), HTMX docs |
| Phase 2 | "Growing Object-Oriented Software" (Freeman), k6 docs |
| Phase 3 | "Enterprise Integration Patterns" (Hohpe), MassTransit docs |
| Phase 4 | "Kubernetes in Action" (Lukša), ArgoCD docs |
