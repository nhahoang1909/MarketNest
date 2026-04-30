# MarketNest — Caching Strategy

> Version: 1.0 | Status: Accepted | Date: 2026-04-29
> Scope: Frontend static asset caching, server-side HTTP response caching, application-layer Redis caching, cross-module communication patterns

---

## Table of Contents

1. [Overview & Goals](#1-overview--goals)
2. [Caching Layers](#2-caching-layers)
3. [Layer 1 — Frontend: Static Asset Caching](#3-layer-1--frontend-static-asset-caching)
4. [Layer 2 — HTTP Output Cache (Razor Pages)](#4-layer-2--http-output-cache-razor-pages)
5. [Layer 3 — Application Cache (Redis via ICacheService)](#5-layer-3--application-cache-redis-via-icacheservice)
6. [Layer 4 — Cross-Module Communication & Data Access](#6-layer-4--cross-module-communication--data-access)
7. [Cache Invalidation Strategy](#7-cache-invalidation-strategy)
8. [Cache Key Convention](#8-cache-key-convention)
9. [OutputCache Policies](#9-outputcache-policies)
10. [Anti-Patterns to Avoid](#11-anti-patterns-to-avoid)

---

## 1. Overview & Goals

MarketNest uses caching at multiple layers, each solving a different performance problem:

| Problem | Layer | Solution |
|---------|-------|---------|
| Static files (CSS/JS/images) re-downloaded every request | Browser cache | `asp-append-version` + `Cache-Control: immutable` |
| HTMX partial responses cached by browser | Middleware | `HtmxNoCacheMiddleware` sets `no-store` |
| Razor Page HTML re-rendered for anonymous users | HTTP OutputCache | Named policies (60s–5m) |
| Commission rate queried per order | Application Redis | `ICacheService.GetOrSetAsync()` |
| Country list needed by Orders from Admin | Cross-module contract | `IReferenceDataReadService` with cache-through |
| Product listing queries expensive | Query-level cache | Redis-backed via `CacheKeys.Catalog.*` |

**Non-goal (Phase 1):** Distributed cache invalidation (pub/sub), cache stampede prevention, CDN overlay. Deferred to Phase 2+.

---

## 2. Caching Layers

```
┌─────────────────────────────────────────────────────────────────┐
│  BROWSER                                                        │
│  Layer 1a: Static assets (CSS/JS) — immutable + hash            │
│  Layer 1b: Media files — max-age=86400                          │
│  Layer 1c: HTMX partials — no-store (never cached)              │
├─────────────────────────────────────────────────────────────────┤
│  ASP.NET CORE                                                   │
│  Layer 2: OutputCache (anonymous Razor Pages) — in-memory       │
│    ├── AnonymousPublic (60s): home, search                      │
│    ├── Storefront (5m): storefront pages                        │
│    └── ProductDetail (2m): product detail pages                 │
├─────────────────────────────────────────────────────────────────┤
│  APPLICATION LAYER                                              │
│  Layer 3: ICacheService (Redis) — business data cache           │
│    ├── CacheKeys.Catalog.*  — product, variant, storefront      │
│    ├── CacheKeys.Cart.*     — count per user                    │
│    ├── CacheKeys.Payments.* — commission per store              │
│    ├── CacheKeys.Identity.* — user preferences                  │
│    └── CacheKeys.Admin.*    — platform config                   │
├─────────────────────────────────────────────────────────────────┤
│  CROSS-MODULE CONTRACTS (in-process)                            │
│  Layer 4: Service contracts returning Snapshot records           │
│    ├── IReferenceDataReadService (Admin → any module)            │
│    ├── IStorefrontReadService  (Catalog → Orders, Payments)     │
│    └── IOrderCreationService   (Cart → Orders)                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 3. Layer 1 — Frontend: Static Asset Caching

### 3.1 Content-Based Cache Busting

All local static files use ASP.NET Core's `asp-append-version="true"` Tag Helper, which appends a content hash query string:

```html
<link rel="stylesheet" href="~/css/site.css" asp-append-version="true"/>
<script src="~/js/app.js" asp-append-version="true" defer></script>
```

Renders as:
```html
<link rel="stylesheet" href="/css/site.css?v=sha256-abc123..."/>
```

### 3.2 StaticFileOptions (Program.cs)

```csharp
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Fingerprinted (?v=...) → cache forever
        if (ctx.Context.Request.Query.ContainsKey("v"))
            headers.CacheControl = "public, max-age=31536000, immutable";

        // Media/font → 1 day
        // .png, .jpg, .webp, .svg, .ico, .woff2, .woff
        else if (IsMediaExtension(ext))
            headers.CacheControl = "public, max-age=86400";

        // Everything else → revalidate
        else
            headers.CacheControl = "no-cache";
    }
});
```

### 3.3 HTMX No-Cache Middleware

`HtmxNoCacheMiddleware` detects `HX-Request` header and forces `Cache-Control: no-store` on the response. Prevents browser back-button showing stale partials.

---

## 4. Layer 2 — HTTP Output Cache (Razor Pages)

### 4.1 Principles

- **Anonymous users**: public pages can cache HTML (home, search, storefront, product)
- **Authenticated users**: NEVER cache (personalized content, cart badge)
- **Admin/Seller pages**: NEVER cache

### 4.2 Policies

Defined in `Program.cs` via `AddOutputCache`, named constants in `CachePolicies`:

| Policy | TTL | Vary By | Condition |
|--------|-----|---------|-----------|
| `AnonymousPublic` | 60s | query: q, category, sort, page | anonymous only |
| `Storefront` | 5m | route: slug | anonymous only |
| `ProductDetail` | 2m | route: slug, productId | anonymous only |

Apply via attribute: `[OutputCache(PolicyName = CachePolicies.Storefront)]`

### 4.3 Invalidation

Use `IOutputCacheStore.EvictByTagAsync("product", ct)` in command handlers when data changes.

---

## 5. Layer 3 — Application Cache (Redis via ICacheService)

### 5.1 CacheKeys Structure

All keys in `CacheKeys` (Base.Common). Convention: `marketnest:{module}:{entity}:{id}`

| Nested Class | Example Key | TTL |
|---|---|---|
| `ReferenceData` | `marketnest:refdata:countries` | 24h |
| `BusinessConfig` | `marketnest:config:order-policy` | 1h |
| `Catalog` | `marketnest:catalog:product:{id}` | 1m–5m |
| `Cart` | `marketnest:cart:count:{userId}` | 30s |
| `Payments` | `marketnest:payments:commission:{storeId}` | 30m |
| `Identity` | `marketnest:identity:prefs:{userId}` | 5m |
| `Admin` | `marketnest:admin:config:global` | 30m |

### 5.2 TTL Presets

| Name | Duration | Use Case |
|------|----------|----------|
| `VeryShort` | 30s | Cart count badge |
| `QuickExpiry` | 1m | Product detail (prices change) |
| `Brief` | 5m | Storefront, user preferences |
| `Medium` | 30m | Commission rates, admin config |
| `BusinessConfig` | 1h | Business policies |
| `VeryLong` | 6h | Category lists |
| `ReferenceData` | 24h | Countries, nationalities |

---

## 6. Layer 4 — Cross-Module Communication & Data Access

### 6.1 Decision: In-Process Service Contracts (Phase 1)

**Question**: When Orders needs data from Catalog/Payments (lots of data, not cacheable), how do modules communicate?

**Options evaluated**:

| Option | Verdict | Why |
|--------|---------|-----|
| gRPC between modules | ❌ Over-engineering | All modules run in-process (monolith). gRPC adds serialization overhead, proto file management, and transport complexity for zero benefit |
| BFF (Backend-For-Frontend) | ❌ Phase 3+ only | BFF makes sense when services are physically separate. In a monolith, it's an unnecessary indirection layer |
| **Service contracts via interfaces** | ✅ **Adopted** | Already the established pattern. Zero-cost in-process calls. Same-process DI. Migrate to gRPC only in Phase 3 when extracting services |

**Pattern**: Cross-module contracts live in `Base.Common/Contracts/`. Owner module implements the interface in its `Infrastructure/Services/`. Consumer module injects the interface — never knows about cache, DB, or implementation details.

### 6.2 Existing Cross-Module Contracts

| Contract | Owner | Consumers | Cached? |
|---|---|---|---|
| `IReferenceDataReadService` | Admin | Identity, Orders, Catalog | ✅ Redis 24h |
| `IStorefrontReadService` | Catalog | Payments, Orders | Module-level cache |
| `IOrderCreationService` | Orders | Cart | No (write operation) |
| `IInventoryService` | Catalog | Orders | No (write operation) |
| `IAuditService` | Auditing | All modules | No (write operation) |

### 6.3 For Large/Dynamic Data (Not Cacheable)

When a consumer needs large or frequently-changing data (e.g., full order with line items):

1. **Define a service contract** in `Base.Common/Contracts/` returning a Snapshot record
2. **Snapshot records** (cross-module DTOs) live alongside the contract — immutable `record` types
3. **Owner module implements** the contract, queries its own DB directly (no cache for volatile data)
4. **Consumer injects** the contract interface — transparent in-process call

Example: `CartSnapshot`, `CartItemSnapshot` already exist in `Base.Common/Contracts/` for the Cart→Orders flow.

### 6.4 Phase 3 Migration Path

When extracting modules into separate services:
1. Service contracts become gRPC/REST API clients
2. Same interface, different implementation (`GrpcStorefrontReadService` instead of `InProcessStorefrontReadService`)
3. Consumer code unchanged — DI swap only
4. BFF pattern may be introduced for frontend-facing aggregation endpoints

---

## 7. Cache Invalidation Strategy

| Data Type | Strategy | Reason |
|-----------|----------|--------|
| Commission rate | Explicit remove + Medium TTL | Financial accuracy |
| Product detail | Explicit remove + QuickExpiry TTL | Prices change |
| Storefront | Explicit remove on update | Slug changes must reflect immediately |
| Country list | TTL only (24h) | Near-immutable |
| Platform config | TTL (30m) + explicit on admin change | Admin-controlled |
| Cart count | VeryShort TTL + explicit on add/remove | Near-realtime |
| User preferences | Explicit remove on save | User expects immediate effect |
| Categories | VeryLong TTL (6h) + explicit on admin CRUD | Rarely changes |

**Rule**: Explicit `RemoveAsync` after successful write operations + TTL as safety net. Never rely solely on TTL for financial data.

---

## 8. Cache Key Convention

```
marketnest:{module}:{entity}:{identifier}
```

- All lowercase, only `a-z`, `0-9`, `-`, `:`
- GUIDs in lowercase hyphenated form
- Max 200 characters
- Prefix grouping enables `RemoveByPrefixAsync("marketnest:catalog:", ct)` for bulk invalidation

---

## 9. OutputCache Policies

Named policy constants in `CachePolicies` class (`MarketNest.Web.Infrastructure`):

```csharp
public static class CachePolicies
{
    public const string AnonymousPublic = "AnonymousPublic";
    public const string Storefront = "Storefront";
    public const string ProductDetail = "ProductDetail";
}
```

---

## 10. Anti-Patterns to Avoid

1. **❌ Cache domain entities** → ✅ Cache DTOs/projections only
2. **❌ Cache without userId in key** → ✅ Always include userId for user-scoped data
3. **❌ Cache write operation results** → ✅ Only cache read operations
4. **❌ Infinite TTL for mutable data** → ✅ Always set TTL + explicit invalidation
5. **❌ Cross-module cache writes** → ✅ Only the owning module writes its cache; consumers read via service contracts
6. **❌ Cache in Domain layer** → ✅ Cache only in Application (query handlers) and Infrastructure layers
7. **❌ gRPC/HTTP between in-process modules** → ✅ Use DI-injected service contracts (Phase 1)

