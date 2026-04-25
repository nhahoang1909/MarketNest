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

- **Phase**: 1 — Modular Monolith (implementation in progress as of 2026-04-25)
- **Branch**: `feature/foundation`
- **Target**: Phase 1 exit by month 3 (real user can browse → register → create storefront → list product → another user buys → order fulfilled)

---

## Solution Structure

| Project | Purpose |
|---------|---------|
| `src/MarketNest.Core` | Shared kernel: base classes, value objects, Result<T,Error> |
| `src/MarketNest.Identity` | Auth: users, roles, JWT, refresh tokens |
| `src/MarketNest.Catalog` | Storefronts, products, variants, inventory |
| `src/MarketNest.Cart` | Cart, CartItem, Redis-backed reservation |
| `src/MarketNest.Orders` | Orders, order lines, fulfillment, shipment state machine |
| `src/MarketNest.Payments` | Payments, payouts, commission |
| `src/MarketNest.Reviews` | Reviews, votes, fraud gate |
| `src/MarketNest.Disputes` | Disputes, messages, resolution |
| `src/MarketNest.Notifications` | Email/SMS dispatch |
| `src/MarketNest.Admin` | Back-office: arbitration, platform config |
| `src/MarketNest.Web` | ASP.NET Core host: Razor Pages + minimal APIs |

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
- **Migrations**: EF Core per-module, auto-applied on startup via `DatabaseInitializer`

---

## Infrastructure Defaults (dev)

- Health endpoint: `GET /health`
- Seq logs: `http://localhost:5341`
- MailHog UI: `http://localhost:8025`
- RabbitMQ management: `http://localhost:15672`

---

## Key Redis Namespaces

```
marketnest:refresh:{tokenId}            TTL: 7d   — refresh tokens
marketnest:blacklist:{tokenId}          TTL: 7d   — revoked tokens
marketnest:ratelimit:{userId}:{endpoint} TTL: 1min — rate limiting
marketnest:cart:{userId}                TTL: 30m  — cart reservation
```

---

## Specification Documents (`docs/`)

| File | Contents |
|------|---------|
| `architecture-requirements.md` | Phased architecture, ADRs, module boundary rules |
| `backend-requirements.md` | Full tech stack, solution structure, CQRS patterns |
| `frontend-requirements.md` | Frontend stack rationale, complete page inventory |
| `code-rules.md` | Naming conventions, C# idioms, DDD principles, banned patterns |
| `domain-design.md` | DDD aggregates, bounded contexts, entity designs |
| `contract-first-guide.md` | CQRS marker interfaces, Result<T,Error>, event contracts |
| `business-logic-requirements.md` | Business rules for all modules |
| `backend-infrastructure-foundations.md` | Base classes, DatabaseInitializer, IDataSeeder |
| `database-infrastructure-utilities.md` | Query builders, specifications, background jobs |
| `be-fe-common-services.md` | HTMX/Alpine integration, HX-Trigger events, form conventions |
| `frontend-component-library.md` | Component registry, form fields, Alpine magic helpers |
| `devops-requirements.md` | Docker Compose topology, GitHub Actions, K8s manifests |
| `advanced-patterns-transaction-auth-fileupload.md` | Saga patterns, auth flows, file uploads |
| `project-planning.md` | Phase timelines, weekly tasks, Phase 1 exit criteria |
