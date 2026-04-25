# Architectural Decisions

Architectural Decision Records (ADRs) for MarketNest. Number sequentially. Keep all entries — they provide historical context.

**Review cadence**: Review quarterly. Mark outdated decisions as `**Status**: Superseded by ADR-XXX` — **never delete** old ADRs. Future developers need the "why" behind legacy code.

**Keep entries concise**: Each ADR should be scannable in 30 seconds. Link to external docs for lengthy analysis.

**When this file exceeds ~20 entries**: Add a Table of Contents at the top.

## Format

### ADR-XXX: Title (YYYY-MM-DD)

**Context:**
- Why the decision was needed

**Decision:**
- What was chosen

**Alternatives Considered:**
- Option → Why rejected

**Consequences:**
- ✅ Benefits
- ❌ Trade-offs

---

## Decisions

### ADR-001: Modular Monolith → Microservices → Kubernetes Phased Architecture (2026-04-25)

**Context:**
- Solo learning project aimed at mastering distributed systems progressively
- Need to deliver a working product early while building toward cloud-native patterns
- Starting with microservices would be premature without a working domain first

**Decision:**
- Phase 1 (months 1–3): Single .NET 10 deployable with schema-per-module PostgreSQL
- Phase 3 (months 6–7): Extract Notification Service, add RabbitMQ/MassTransit + YARP gateway
- Phase 4 (months 8–9): Kubernetes (kind locally, AKS/EKS cloud) with Helm + ArgoCD

**Alternatives Considered:**
- Start with microservices → Rejected: too complex before domain is understood
- Stay as monolith → Rejected: doesn't meet learning goal of distributed systems

**Consequences:**
- ✅ Working product by month 3
- ✅ Progressive complexity — each phase builds on established patterns
- ✅ Clear exit criteria per phase
- ❌ Some refactoring required at phase boundaries (in-process → RabbitMQ events)

---

### ADR-002: Razor Pages + HTMX + Alpine.js (No SPA Framework) (2026-04-25)

**Context:**
- Need interactive UI without the complexity of a full SPA build pipeline
- Server-side rendering preferred for SEO and simplicity
- Want reactive components without shipping a large JS framework

**Decision:**
- Razor Pages for server-rendered HTML
- HTMX 2 for partial page updates (replaces full-page reloads)
- Alpine.js 3 for client-side reactive components
- Tailwind CSS 4 for styling

**Alternatives Considered:**
- React/Next.js → Rejected: adds SPA complexity to a backend-focused learning project
- Blazor Server → Rejected: SignalR overhead for every interaction; less standard
- Vue.js → Rejected: same SPA complexity argument

**Consequences:**
- ✅ Simple mental model: HTML over the wire
- ✅ No separate frontend build/deploy pipeline in Phase 1
- ✅ Progressive enhancement by default
- ❌ Less component reuse compared to React ecosystem

---

### ADR-003: Result<T, Error> — No Exceptions for Business Failures (2026-04-25)

**Context:**
- Need explicit error handling that forces callers to handle failures
- Exceptions are expensive and their propagation is implicit
- Railway-oriented programming makes error flow visible in the type system

**Decision:**
- All application-layer methods return `Result<T, Error>`
- Exceptions reserved for truly exceptional infrastructure failures (DB unreachable, etc.)
- Error codes use `DOMAIN.ENTITY_ERROR` format (e.g., `ORDER.NOT_FOUND`)

**Alternatives Considered:**
- Throw exceptions everywhere → Rejected: implicit propagation, hard to reason about
- Nullable returns → Rejected: doesn't distinguish between "not found" and "failed"

**Consequences:**
- ✅ All failure paths are explicit and compiler-enforced
- ✅ Clean error codes for API responses
- ❌ Slightly more verbose than try/catch
- ❌ Callers must unwrap results (but this is the point)

---

### ADR-004: EF Core with Schema-Per-Module Boundary Enforcement (2026-04-25)

**Context:**
- Need physical enforcement of module boundaries in Phase 1 monolith
- Modules must not query each other's tables even though they share one DB
- Migrating to separate DBs in Phase 3 should be low-friction

**Decision:**
- Each module owns a separate PostgreSQL schema (e.g., `identity.*`, `orders.*`)
- Each module has its own `DbContext` — no cross-schema joins in EF
- Cross-module data needs go through service interfaces or domain events

**Alternatives Considered:**
- Single shared DbContext → Rejected: too easy to accidentally join across modules
- Separate databases from day one → Rejected: distributed transactions complexity in Phase 1

**Consequences:**
- ✅ Module isolation enforced by DB schema boundaries
- ✅ Easy to split to separate DBs in Phase 3
- ❌ Slight duplication of shared lookup data (e.g., user display name in orders)

---

### ADR-005: No Magic Strings / Magic Numbers — All Literals Must Be Named Constants or Enums (2026-04-25)

**Context:**
- Codebase will grow across 10+ modules; scattered string/numeric literals become hard to refactor and easy to mistype
- Repeated values like Redis key prefixes, route paths, commission rates, and retry counts need a single source of truth

**Decision:**
- Every string literal used more than once and every unexplained numeric literal must be extracted to a `const`, `static readonly`, enum, or strongly-typed configuration option
- Exceptions: `0`, `1`, `-1`, `string.Empty`, and obvious boolean comparisons
- Rule documented in `docs/code-rules.md` §2.5 and enforced via PR checklist

**Alternatives Considered:**
- Rely on code review alone → Rejected: too easy to miss, inconsistent enforcement
- Roslyn analyzer (e.g., CA1802/CA1805) → Considered for future; manual rule sufficient for now

**Consequences:**
- ✅ Single source of truth for all repeated values — rename once, change everywhere
- ✅ Reduces typo-related bugs (e.g., misspelled Redis key prefix)
- ✅ Improves readability — named constants communicate intent
- ❌ Slightly more boilerplate for one-off constants
