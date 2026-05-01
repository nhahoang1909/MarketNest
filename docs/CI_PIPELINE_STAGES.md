# CI Pipeline Stages - 3-Stage Test Strategy

## Overview

The CI pipeline is restructured into **5 main stages** with **3 dedicated test stages** and sequential gating to provide early feedback and efficient resource utilization.

## Pipeline Architecture

### Stage 1: Restore & Prepare
- **Job**: `restore`
- **Purpose**: Cache NuGet packages globally
- **Runs**: Once per workflow, in parallel with nothing
- **Time**: ~2-3 minutes

### Stage 2: Build
- **Parallel Jobs**: `build-base`, `build-identity`, `build-catalog`, `build-cart`, `build-orders`, `build-payments`, `build-reviews-disputes`, `build-notifications`, `build-admin`, `build-auditing`, `build-promotions`, `build-analyzers`, `build-web`
- **Purpose**: Compile all modules individually  
- **Depends On**: `restore`
- **Early Feedback**: Each module fails independently = clear visibility
- **Time**: ~8-12 minutes (parallel)

---

## 3-Stage Test Strategy

### **Stage 3A: Architecture & Analyzer Tests** (Fast, Fail-Fast)

**Jobs**:
- `test-architecture` — NetArchTest rules (layer boundaries, module isolation)
- `test-analyzers` — Roslyn analyzer rule suite (MN001-MN036)

**Gate**: `stage1-gate`

**Depends On**: `build-web` (architecture tests) + `build-analyzers` (analyzer tests)

**Purpose**:
- Catch architecture violations early
- Catch analyzer rule violations early
- Fail fast before running expensive unit/integration tests
- These are the cheapest tests to run

**Time**: ~2-3 minutes

**Why First**:
- If your architecture is broken, there's no point running unit tests
- Immediate feedback = faster iteration

---

### **Stage 3B: Unit Tests** (Per-Module, Parallel)

**Jobs** (all depend on `stage1-gate`):
- `test-unit-base-core`
- `test-unit-identity`
- `test-unit-catalog`
- `test-unit-cart`
- `test-unit-orders`
- `test-unit-payments`
- `test-unit-admin-auditing`
- `test-unit-promotions`
- `test-unit-reviews-disputes-notifications`

**Gate**: `stage2-gate`

**Purpose**:
- Test module-level business logic
- Filter by namespace to isolate module tests
- Run in parallel for speed
- More expensive than architecture tests but cheaper than integration tests

**Time**: ~8-15 minutes (parallel)

**Details**:
- Each job runs only tests matching its module namespace filter
- Example: `test-unit-catalog` runs tests where `FullyQualifiedName~MarketNest.Catalog`
- No external dependencies (no DB, no Redis required)
- Can fail without affecting other modules

**Why Second**:
- Unit tests validate individual module behavior
- Must pass architecture tests first (malformed code won't compile/test)
- More numerous than architecture tests
- Blocks integration tests (we want to catch issues early)

---

### **Stage 3C: Integration Tests** (Resource-Intensive)

**Job**: `test-integration`

**Gate**: `stage2-gate` (depends on all unit tests)

**Services Spun Up**:
- PostgreSQL 16 (for database contracts)
- Redis 7 (for caching contracts)

**Purpose**:
- End-to-end tests across module boundaries
- Validates database migrations
- Validates cross-module event publishing
- Tests real infrastructure interactions

**Environment Variables**:
```
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=marketnest_test;Username=mn;Password=mn_secret
ConnectionStrings__ReadConnection=
Redis__ConnectionString=localhost:6379
```

**Time**: ~10-20 minutes (includes service startup)

**Why Last**:
- Integration tests are slow (spin up containers, run migrations, seed data)
- No point running if unit tests fail
- Validates cross-module contracts
- Catches real deployment issues

---

## Final Verification & Deployment

### **Verify Gate** (`verify`)

**Depends On**:
- `stage1-gate` (architecture + analyzers passed)
- `stage2-gate` (unit tests passed)
- `test-integration` (integration tests passed)

**Purpose**:
- Single aggregation job for branch protection rules
- Set this as the required check in GitHub branch protection
- Prevents merging until all test stages pass

**Output**: ✅ Architecture tests, unit tests, and integration tests all passed. Ready for docker build.

---

### **Docker Build & Push** (`docker-build`)

**Depends On**: `verify`

**Conditions**: Only runs on `main` or `p1-main` branches

**Purpose**:
- Build Docker image tagged with git SHA
- Push to GHCR (GitHub Container Registry)
- Only builds if verify gate passes

**Time**: ~5-8 minutes

---

## Complete Execution Flow

```
┌─────────────────────────────────────────────────────────────────┐
│ STAGE 1: RESTORE                                                │
│   └─ restore (NuGet cache)                                      │
└─────────────┬───────────────────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────────────────┐
│ STAGE 2: BUILD (Parallel)                                       │
│   ├─ build-base, build-core, build-identity, ...               │
│   └─ build-web (depends on all modules)                        │
└─────────────┬───────────────────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────────────────┐
│ STAGE 3A: ARCHITECTURE TESTS (Fast Feedback)                    │
│   ├─ test-architecture (NetArchTest)                           │
│   ├─ test-analyzers (Roslyn rules)                             │
│   └─ stage1-gate (aggregation)                                 │
└─────────────┬───────────────────────────────────────────────────┘
              │
     [FAIL? Stop here - fast feedback loop]
              │
┌─────────────▼───────────────────────────────────────────────────┐
│ STAGE 3B: UNIT TESTS (Module-Level, Parallel)                   │
│   ├─ test-unit-base-core                                       │
│   ├─ test-unit-identity                                        │
│   ├─ test-unit-catalog                                         │
│   ├─ ... (9 jobs in parallel)                                  │
│   └─ stage2-gate (aggregation)                                 │
└─────────────┬───────────────────────────────────────────────────┘
              │
     [FAIL? Stop here - don't waste integration test time]
              │
┌─────────────▼───────────────────────────────────────────────────┐
│ STAGE 3C: INTEGRATION TESTS (End-to-End)                        │
│   ├─ PostgreSQL service started                                │
│   ├─ Redis service started                                     │
│   └─ test-integration (runs full test suite)                   │
└─────────────┬───────────────────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────────────────┐
│ VERIFY GATE                                                     │
│   └─ verify (single aggregation job for branch protection)     │
└─────────────┬───────────────────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────────────────┐
│ DOCKER BUILD & PUSH (only on main/p1-main)                      │
│   └─ docker-build → GHCR                                        │
└─────────────────────────────────────────────────────────────────┘
```

---

## Benefits of This Strategy

| Aspect | Benefit |
|--------|---------|
| **Fast Feedback** | Architecture violations caught in 2-3 minutes |
| **Resource Efficiency** | Don't spin up containers unless basic tests pass |
| **Parallel Execution** | 11 unit test jobs run simultaneously |
| **Clear Visibility** | Each job failure is independently visible |
| **Module Isolation** | Module tests filtered by namespace, no cross-pollution |
| **Branch Protection** | Set `verify` as the only required check (simplifies rules) |
| **Sequential Gating** | Each stage gates the next = prevents wasted compute |
| **Audit Trail** | All test results uploaded as artifacts |

---

## GitHub Branch Protection

Set `verify` as the **only required check**:

```
Status checks that are required to pass before merging:
  ☑ ✅ All checks passed
```

This single check aggregates all test stages, making the rule maintenance simple.

---

## Typical Execution Times

| Stage | Time (parallel) | Notes |
|-------|-----------------|-------|
| Restore + Build | ~10-15 min | Includes cache hits/misses |
| Architecture Tests | ~2-3 min | Fast, no DB/Redis |
| Unit Tests | ~8-15 min | Parallel, no external infra |
| Integration Tests | ~10-20 min | Includes container startup |
| **Total (Green)** | **~25-40 min** | All stages in sequence |
| **Total (Red at Stage 3A)** | **~15 min** | Fast feedback on arch violations |

---

## Reverting to Old Structure

If needed, the old structure (all tests depending on `build-web`) can be restored by:
1. Changing each unit test `needs:` from `stage1-gate` to `build-web`
2. Removing `stage1-gate` and `stage2-gate` jobs
3. Updating `verify` `needs:` to list all test jobs individually

However, the new 3-stage strategy is significantly more efficient.

