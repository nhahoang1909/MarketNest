# Key Facts

Non-sensitive project constants, endpoints, and configuration. **Never store passwords, API keys, or secrets here** — use `.env` or user-secrets for those.

### What belongs here vs. what doesn't

| ❌ Never store here | ✅ Safe to store here |
|---|---|
| Passwords, API keys, access/refresh tokens | Hostnames and public URLs |
| Private keys, service account keys | Port numbers (`5432`, `6379`, etc.) |
| OAuth client secrets | Project IDs, environment names (`staging`, `prod`) |
| DB connection strings **with passwords** | Non-sensitive config (timeouts, retry counts, feature flags) |
| SSH keys, VPN credentials | Service account email addresses |

> Secrets belong in **`.env`** (gitignored), **cloud secrets managers** (GCP/AWS/Azure), **CI/CD variables**, or **Kubernetes Secrets**.
> If secrets are accidentally committed, **rotate them immediately** — removing from git history isn't enough.

---

## Current Phase

- **Phase**: 1 — Modular Monolith (implementation in progress as of 2026-04-29)
- **Branch**: `p1-main-nhahoang` (working branch; PRs target `p1-main`)
- **Target**: Phase 1 exit by month 3 (real user can browse → register → create storefront → list product → another user buys → order fulfilled)

---

## Solution Structure

| Project | Purpose |
|---------|---------|
| `src/Base/MarketNest.Base.Api` | Base API controller abstractions (`ReadApiV1ControllerBase`, `WriteApiV1ControllerBase`) |
| `src/Base/MarketNest.Base.Common` | Shared contracts: `IBaseQuery`, `ICacheService`, `CacheKeys`, cross-module service interfaces, `IReferenceDataReadService`, Tier-2 config contracts |
| `src/Base/MarketNest.Base.Domain` | Shared domain primitives: `Entity<T>`, `AggregateRoot`, `ValueObject`, `ReferenceData` base |
| `src/Base/MarketNest.Base.Infrastructure` | Shared infra: `IAppLogger<T>`, `AppLogger<T>`, `LogEventId` enum, `BaseRepository<TEntity,TKey,TContext>`, `IBaseRepository<TEntity,TKey>`, `BaseQuery<TEntity,TKey,TContext>`, `DddModelBuilderExtensions` |
| `src/Base/MarketNest.Base.Utility` | Utility helpers: slug generation, date extensions |
| `src/MarketNest.Core` | Shared kernel: `Result<T,Error>`, `Error`, CQRS interfaces, `IModuleDbContext`, `IDataSeeder`, validation extensions, domain constants, status names |
| `src/MarketNest.Identity` | Auth: users, roles, JWT, refresh tokens, user preferences, notification preferences |
| `src/MarketNest.Catalog` | Storefronts, products, variants, inventory |
| `src/MarketNest.Cart` | Cart, CartItem, Redis-backed reservation |
| `src/MarketNest.Orders` | Orders, order lines, fulfillment, shipment state machine; owns `OrderPolicyConfig` |
| `src/MarketNest.Payments` | Payments, payouts, commission; owns `CommissionPolicy` |
| `src/MarketNest.Promotions` | Vouchers, voucher usage, discount calculation |
| `src/MarketNest.Reviews` | Reviews, votes, fraud gate |
| `src/MarketNest.Disputes` | Disputes, messages, resolution |
| `src/MarketNest.Notifications` | Email/SMS dispatch |
| `src/MarketNest.Admin` | Back-office: reference data (Country, Gender, Category…), admin-config UI, arbitration |
| `src/MarketNest.Auditing` | Audit logs, login events; `AuditableInterceptor` + `AuditBehavior<,>` |
| `src/MarketNest.Web` | ASP.NET Core host: Razor Pages + minimal APIs, `DatabaseInitializer`, middleware, `SharedViewPaths` constants |
| `src/MarketNest.Analyzers` | Roslyn analyzers: 17 MN rules (MN001–MN017), 5 code-fix providers |

---

## Local Development Ports (Docker Compose)

| Service | Port |
|---------|------|
| ASP.NET Core app | 5000 / 5001 (HTTPS) |
| PostgreSQL | 5432 |
| Redis | 6379 |
| RabbitMQ management UI | 15672 |
| MailHog (email) | 8025 |
| Seq (structured logs) | 5341 |
| Nginx | 80 / 443 |

---

## Database

- **Engine**: PostgreSQL 16
- **Dev credentials**: user `mn` / database `mn` (password in `.env` — see `.env.example`)
- **Schema per module**: `identity.*`, `catalog.*`, `cart.*`, `orders.*`, `payments.*`, `reviews.*`, `disputes.*`, `notifications.*`, `admin.*`
- **System tables**: `public.__auto_migration_history`, `public.__seed_history` (tracking tables for `DatabaseInitializer`)
- **Migrations**: EF Core per-module, auto-applied on startup via `DatabaseInitializer`

---

## Infrastructure Defaults (dev)

- Health endpoint: `GET /health`
- OpenAPI spec: `GET /openapi/v1.json`
- Scalar API docs (interactive UI): `/scalar/v1` (dev only)
- Seq logs: `http://localhost:5341`
- MailHog UI: `http://localhost:8025`
- RabbitMQ management: `http://localhost:15672`
- API contract markdown: `docs/api-contract.md` (auto-generated on startup in dev mode)

---

## Key Redis Namespaces

```
marketnest:refresh:{tokenId}              TTL: 7d    — refresh tokens
marketnest:blacklist:{tokenId}            TTL: 7d    — revoked tokens
marketnest:ratelimit:{userId}:{endpoint}  TTL: 1min  — rate limiting
marketnest:cart:{userId}                  TTL: 30m   — cart reservation
marketnest:refdata:{entity}               TTL: 24h   — Tier 1 reference data (IReferenceDataReadService)
marketnest:config:orders                  TTL: 1h    — Tier 2 OrderPolicyConfig
marketnest:config:payments                TTL: 1h    — Tier 2 CommissionPolicy
marketnest:config:catalog                 TTL: 1h    — Tier 2 StorefrontPolicyConfig
marketnest:config:reviews                 TTL: 1h    — Tier 2 ReviewPolicyConfig
```

---

## Specification Documents (`docs/`)

| File | Contents |
|------|---------|
| `architecture.md` | Phased architecture, ADRs, module boundaries, solution structure, project layout |
| `backend-patterns.md` | Tech stack, CQRS contracts, `Result<T,Error>`, base classes, services, seeding, background jobs |
| `backend-infrastructure.md` | Query utilities, caching, transactions, UoW, `[Access]` permissions, file uploads, testing |
| `domain-and-business-rules.md` | DDD aggregates, bounded contexts, entity designs, business rules for all modules |
| `frontend-guide.md` | Frontend stack, page inventory, HTMX/Alpine patterns, component library, BE-FE contracts |
| `code-rules.md` | Naming conventions, C# idioms, DDD principles, banned patterns |
| `devops-requirements.md` | Docker Compose topology, GitHub Actions, K8s manifests |
| `analyzers.md` | Roslyn analyzer reference: all 17 MN rules, suppression patterns, adding new rules |
| `test-driven-design.md` | TDD guidelines, unit/integration/architecture test patterns |
| `api-contract.md` | Auto-generated from OpenAPI spec by `ApiContractGenerator` on startup (dev only) |
