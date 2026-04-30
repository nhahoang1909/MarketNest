---
name: database-review
description: >
  Scan the entire MarketNest codebase to review database layer quality (.NET 10, EF Core 10,
  PostgreSQL 16, Redis 7). Use this skill when the user wants to: check EF Core migrations,
  find N+1 queries, audit PostgreSQL indexes, analyze query plans with EXPLAIN ANALYZE, check
  schema-per-module isolation, review Redis key naming and TTL strategy, find missing indexes,
  check AsNoTracking usage, detect tracking queries in read paths, or says anything like
  "review database", "check query", "optimize EF Core", "N+1", "index audit",
  "Redis TTL", "migration review", "check schema", "slow query".
  Activate when the user uploads migration, DbContext, entity config, or repository files.
compatibility:
  tools: [bash, read_file, write_file, list_files, grep_search, run_in_terminal]
  agents: [claude-code, gemini-cli, cursor, continue, aider, copilot]
  stack: [.NET 10, EF Core 10, PostgreSQL 16, Redis 7, StackExchange.Redis 2.x]
---

# Database Review Skill — MarketNest

This skill scans the entire database layer of MarketNest: EF Core entity configs, migrations,
query handlers, repositories, and Redis services — then reports findings classified by severity
with specific fixes.

---

## Execution Flow (mandatory order)

```
Phase 1: SCAN      → Locate all database-related files in the solution
Phase 2: ANALYZE   → Check each category against the checklist
Phase 3: REPORT    → Classify as 🔴 CRITICAL / 🟡 HIGH / 🟢 MEDIUM with file:line
Phase 4: FIX       → Propose before/after fix (confirm before applying)
Phase 5: VERIFY    → Re-check after fixes are applied
```

---

## Phase 1: SCAN — Identify Files to Review

### 1.1 Locate all database-related files

**PowerShell:**
```powershell
# Find all DbContext files
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern 'DbContext|DbSet<' -List |
  Select-Object Path

# Find all Entity Configuration files
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.Name -match 'Configuration' -and $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-Object FullName

# Find all Migration files (excluding .Designer.cs)
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -match '\\Migrations\\' -and $_.Name -notmatch '\.Designer\.cs' -and $_.FullName -notmatch '\\(bin|obj)\\' } |
  Sort-Object Name

# Find all Repository files
Get-ChildItem -Recurse -Filter *Repository.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | Select-Object FullName

# Find all Query handler files
Get-ChildItem -Recurse -Filter *QueryHandler.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | Select-Object FullName

# Find all Redis service files
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern 'IDatabase|StackExchange\.Redis|IConnectionMultiplexer|RedisKey|KeyExpire' -List |
  Select-Object Path
```

### 1.2 Collect quick statistics

```powershell
# Count current migrations
(Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -match '\\Migrations\\' -and $_.Name -notmatch '\.Designer\.cs' -and $_.FullName -notmatch '\\(bin|obj)\\' }).Count

# Most recent migrations
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -match '\\Migrations\\' -and $_.Name -notmatch '\.Designer\.cs' } |
  Sort-Object Name | Select-Object -Last 5 Name

# Count DbSet declarations
Select-String -Path src/**/*.cs -Pattern 'DbSet<' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' } | Measure-Object | Select-Object Count

# Count .Include() calls (potential N+1 or over-fetch)
(Select-String -Path src/**/*.cs -Pattern '\.Include\(|\.ThenInclude\(' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }).Count
```

---

## Phase 2: ANALYZE — Detailed Checklist

---

### 2.1 EF Core Migration Review

#### Check migration naming convention

```powershell
# Convention: YYYYMMDD_HHmm_DescriptiveTitle
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -match '\\Migrations\\' -and $_.Name -notmatch '\.Designer\.cs' } |
  Where-Object { $_.Name -notmatch '^\d{8}_\d{4}_[A-Z][A-Za-z]+' } |
  Select-Object Name
# Output: migration files that violate naming convention
```

**Migration checklist:**
- [ ] Migration name is descriptive: `AddOrderDisputeTable`, `AddIndexProductStoreId` — not `Migration1`, `Update2`
- [ ] Every migration has a complete `Down()` — important for rollbacks
- [ ] No raw `MigrationBuilder.Sql()` without an explanatory comment
- [ ] No non-nullable column added to a table with existing data without a default value
- [ ] No direct column drop/rename (always deprecate first, drop later)
- [ ] `HasPrecision(18, 2)` for every `decimal` column related to money

```powershell
# Find decimal columns missing precision
Select-String -Path src/**/*.cs -Pattern 'decimal|price|amount|commission|payout' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' -and $_.Line -notmatch 'HasPrecision' } |
  Where-Object { $_.Line -match 'Property|Column' } |
  Select-Object Path, Line | Select-Object -First 20

# Find migrations with empty Down()
Select-String -Path src/**/*.cs -Pattern 'protected override void Down' -Recurse -Context 0,3 |
  Where-Object { $_.Context.PostContext -match '^\s*\}' } |
  Select-Object Path
```

#### Check schema-per-module isolation

```powershell
# Each module must only ToTable() its own schema
Select-String -Path src/**/*.cs -Pattern '\.ToTable\(' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' } |
  Select-Object Path, Line | Select-Object -First 40

# Cross-schema violation: Orders module must not reference "catalog" schema
Select-String -Path src/MarketNest.Orders/**/*.cs -Pattern '\.ToTable\(' -Recurse |
  Where-Object { $_.Line -notmatch '"orders"' -and $_.Path -notmatch '\\(bin|obj)\\' } |
  Select-Object Path, Line

Select-String -Path src/MarketNest.Catalog/**/*.cs -Pattern '\.ToTable\(' -Recurse |
  Where-Object { $_.Line -notmatch '"catalog"' -and $_.Path -notmatch '\\(bin|obj)\\' } |
  Select-Object Path, Line
```

**Violation example:**
```csharp
// ❌ Orders module referencing catalog schema
builder.ToTable("products", "catalog");  // in Orders module — WRONG

// ✅ Each module only references its own schema
builder.ToTable("order_lines", "orders");
```

#### Check soft delete query filter

```powershell
# All entities with soft delete must have HasQueryFilter
Select-String -Path src/**/*.cs -Pattern 'IsDeleted|DeletedAt' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' -and $_.Line -notmatch 'HasQueryFilter' } |
  Where-Object { $_.Line -match 'Property|Column' } |
  Select-Object Path, Line | Select-Object -First 20

# Verify HasQueryFilter is applied
Select-String -Path src/**/*.cs -Pattern 'HasQueryFilter' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' } | Select-Object Path, Line
```

---

### 2.2 N+1 Query Detection

N+1 is the most common and most dangerous EF Core problem.

#### Pattern 1 — Lazy loading inside a loop

```powershell
# Find navigation property access inside foreach / Select (potential lazy load)
grep -rn "foreach\|\.Select\|\.ForEach" src/ --include="*.cs" \
  | grep -v "bin/\|obj/\|test\|Test" -A3 | head -30

# Find virtual navigation properties (lazy loading enabled)
Select-String -Path src/**/*.cs -Pattern 'public virtual|virtual ICollection|virtual IEnumerable' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' } |
  Select-Object Path, Line | Select-Object -First 20
```

**Dangerous pattern:**
```csharp
// ❌ N+1: each order.Lines triggers an additional SELECT
var orders = await db.Orders.ToListAsync();
foreach (var order in orders)
{
    var lineCount = order.Lines.Count; // LAZY LOAD → N queries!
}

// ✅ Eager load
var orders = await db.Orders
    .Include(o => o.Lines)
    .ToListAsync();
```

#### Pattern 2 — Include inside Write path (wasteful)

```powershell
# Command handlers should not Include for aggregate loading
Select-String -Path src/**/*.cs -Pattern '\.Include\(|\.ThenInclude\(' -Recurse |
  Where-Object { $_.Path -match 'CommandHandler' -and $_.Path -notmatch '\\(bin|obj)\\' } |
  Select-Object Path, Line
```

**Rule**: Command handler → use `GetByIdAsync()` from repository (loads aggregate). Query handler → use `AsNoTracking().Select()` to project directly to DTO; only use Include when truly necessary.

#### Pattern 3 — Missing AsNoTracking in Query handler

```powershell
# Query handlers that do not call AsNoTracking
Get-ChildItem -Recurse -Filter *QueryHandler.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  Where-Object { -not (Select-String -Path $_.FullName -Pattern 'AsNoTracking' -Quiet) } |
  Select-Object FullName
```

**Fix:**
```csharp
// ❌ Tracking query in read path — wastes memory and is slower
var products = await db.Products
    .Where(p => p.Status == ProductStatus.Active)
    .ToListAsync(ct);

// ✅ No tracking + projection to DTO
var products = await db.Products
    .AsNoTracking()
    .Where(p => p.Status == ProductStatus.Active)
    .Select(p => new ProductListItemDto(p.Id, p.Title, p.Store.Slug))
    .ToListAsync(ct);
```

#### Pattern 4 — Cartesian explosion with multiple Includes

```powershell
# Multiple parallel Include() calls on collections → Cartesian product
Select-String -Path src/**/*.cs -Pattern '\.Include\(.*\).*\.Include\(' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' } |
  Select-Object Path, Line
```

```csharp
// ❌ Cartesian explosion: two collection navigations in same query
var orders = await db.Orders
    .Include(o => o.Lines)     // collection
    .Include(o => o.Shipments) // collection → multiplied rows!
    .ToListAsync();

// ✅ Split query — EF Core 5+
var orders = await db.Orders
    .Include(o => o.Lines)
    .Include(o => o.Shipments)
    .AsSplitQuery()  // splits into multiple queries instead of a JOIN
    .ToListAsync();
```

---

### 2.3 PostgreSQL Index Audit

#### Scan entity configuration for missing indexes

```powershell
# List all declared indexes
Select-String -Path src/**/*.cs -Pattern 'HasIndex|HasForeignKey|\.HasOne\(|\.HasMany\(' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' } |
  Select-Object Path, Line | Select-Object -First 40

# Find foreign key columns without indexes (often forgotten)
Select-String -Path src/**/*.cs -Pattern 'HasForeignKey|ForeignKey|\.WithOne\|\.WithMany' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' } |
  Select-Object Path, Line | Select-Object -First 30
```

#### Required index coverage for MarketNest

Read each entity configuration and verify the following columns have indexes:

**catalog schema:**
```
products:         store_id, status, created_at (desc), search_vector (GIN — full-text)
product_variants: product_id, price
inventory_items:  variant_id
```

**orders schema:**
```
orders:      buyer_id, status, created_at (desc), seller_id
order_lines: order_id, variant_id
fulfillments: order_id
shipments:   order_id
```

**identity schema:**
```
users:          email (unique), normalized_email (unique)
refresh_tokens: token_hash (unique), user_id, expires_at
```

**payments schema:**
```
payments:    order_id, status
payouts:     seller_id, status
commissions: order_id
```

**reviews schema:**
```
reviews:      product_id, buyer_id, created_at (desc)
review_votes: review_id, user_id (composite unique)
```

**disputes schema:**
```
disputes:         order_id, status
dispute_messages: dispute_id, created_at
```

**Fix pattern:**
```csharp
// ❌ Missing indexes on buyer_id and status
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", "orders");
        builder.HasKey(o => o.Id);
        // No HasIndex declared!
    }
}

// ✅ Full index coverage
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", "orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Total).HasPrecision(18, 2);

        builder.HasIndex(o => o.BuyerId);
        builder.HasIndex(o => new { o.BuyerId, o.Status });   // composite
        builder.HasIndex(o => new { o.SellerId, o.Status });
        builder.HasIndex(o => o.CreatedAt);

        // Partial index — active orders only
        builder.HasIndex(o => o.Status)
               .HasFilter("status NOT IN ('completed', 'cancelled')");
    }
}
```

#### Full-text search index (PostgreSQL tsvector)

```powershell
# Check whether Product has a tsvector GIN index for search
Select-String -Path src/**/*.cs -Pattern 'tsvector|GinIndex|GIN|to_tsvector' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' } | Select-Object Path, Line
```

```csharp
// ✅ GIN index for full-text search (PostgreSQL)
builder.HasIndex(p => p.SearchVector).HasMethod("GIN");

builder.Property(p => p.SearchVector)
       .HasComputedColumnSql(
           "to_tsvector('english', coalesce(title,'') || ' ' || coalesce(description,''))",
           stored: true);
```

---

### 2.4 EXPLAIN ANALYZE — Query Plan Analysis

Use when you can connect to PostgreSQL (local Docker Compose):

```powershell
# Connect to PostgreSQL in Docker
$containerId = docker ps -q -f name=postgres
docker exec -it $containerId psql -U mn -d marketnest
```

```sql
-- Enable timing and buffers
\timing on

-- Analyze most common query: browse products
EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT)
SELECT p.id, p.title, s.slug, MIN(v.price)
FROM catalog.products p
JOIN catalog.storefronts s ON s.id = p.store_id
JOIN catalog.product_variants v ON v.product_id = p.id
WHERE p.status = 'Active'
  AND p.is_deleted = false
GROUP BY p.id, p.title, s.slug
ORDER BY p.created_at DESC
LIMIT 20 OFFSET 0;
-- ⚠️ Watch for: Seq Scan on large table → needs index
-- ⚠️ Watch for: Hash Join with badly underestimated rows → run ANALYZE
-- ✅ Want to see: Index Scan, Bitmap Index Scan

-- Orders by buyer
EXPLAIN (ANALYZE, BUFFERS)
SELECT o.id, o.status, o.total, o.created_at
FROM orders.orders o
WHERE o.buyer_id = 'USER_GUID_HERE'
  AND o.status != 'Cancelled'
ORDER BY o.created_at DESC;
-- ⚠️ Seq Scan → needs index on (buyer_id, created_at)

-- Refresh token lookup (critical path)
EXPLAIN (ANALYZE, BUFFERS)
SELECT rt.* FROM identity.refresh_tokens rt
WHERE rt.token_hash = 'HASH_HERE'
  AND rt.expires_at > NOW();
-- ✅ Should be Index Scan (unique index on token_hash)
```

**Reading EXPLAIN output:**

| Symbol | Meaning | Action |
|---|---|---|
| `Seq Scan` on table > 1000 rows | Full table scan | Add index on WHERE column |
| `cost=xxx..yyy` with large yyy | Expensive query | Review JOINs, indexes, SELECT * |
| `rows=1 actual rows=5000` | Large cardinality estimate error | Run `ANALYZE table_name` |
| `Hash Join` | OK for medium size | Check execution time |
| `Nested Loop` with many rows | Dangerous if outer is large | Check inner table has index |
| `Buffers: shared read=xxx` high | Cache miss | Consider caching |

```sql
-- Update statistics after adding indexes
ANALYZE catalog.products;
ANALYZE orders.orders;

-- Check index usage after creation
SELECT schemaname, tablename, indexname, idx_scan, idx_tup_read, idx_tup_fetch
FROM pg_stat_user_indexes
WHERE schemaname IN ('catalog', 'orders', 'identity', 'payments')
ORDER BY idx_scan DESC;

-- Find unused indexes (candidates for removal)
SELECT indexrelid::regclass AS index_name, relid::regclass AS table_name, idx_scan
FROM pg_stat_user_indexes
WHERE idx_scan = 0 AND schemaname NOT IN ('pg_catalog', 'information_schema')
ORDER BY table_name;
```

---

### 2.5 Redis Key Naming & TTL Strategy

#### Scan all Redis service code

```powershell
# Find all Redis key construction
Select-String -Path src/**/*.cs -Pattern 'marketnest:|"cart:|"session:|"ratelimit:|"refresh:|"blacklist:' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' } | Select-Object Path, Line | Select-Object -First 30

# Find all Redis writes without TTL (potential memory leak)
Select-String -Path src/**/*.cs -Pattern 'StringSetAsync|HashSetAsync|SetAsync' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' -and $_.Line -notmatch 'TimeSpan|expiry|Expiry|ttl|TTL|KeyExpire' } |
  Select-Object Path, Line | Select-Object -First 20
```

#### Standard MarketNest Redis key convention

```
marketnest:cart:{userId}:reservation:{productVariantId}   TTL: 15 min  (900s)
marketnest:session:{sessionId}                            TTL: 24 h    (86400s)
marketnest:ratelimit:{userId}:{endpoint}                  TTL: 1 min   (60s)
marketnest:refresh:{tokenId}                              TTL: 7 d     (604800s)
marketnest:blacklist:{tokenId}                            TTL: 7 d     (604800s)
```

**Redis review checklist:**
- [ ] Key format uses `:` as separator (not `/` or `.`)
- [ ] TTL values use constants, not magic numbers
- [ ] Every `StringSetAsync` / `HashSetAsync` includes an expiry
- [ ] Atomic operations use `When.NotExists` or Lua script

**Violation examples:**
```csharp
// ❌ Wrong key format — uses / instead of :
var key = $"marketnest/cart/{userId}/reservation/{variantId}";

// ❌ Magic number TTL
await db.StringSetAsync(key, value, TimeSpan.FromSeconds(900));

// ❌ No TTL → memory leak
await db.StringSetAsync(key, value);

// ❌ Non-atomic check-then-set → race condition
var existing = await db.StringGetAsync(key);
if (existing.IsNullOrEmpty)
    await db.StringSetAsync(key, value, TimeSpan.FromSeconds(900));

// ✅ Correct: constant + correct key format + NX flag (atomic)
private const int ReservationTtlSeconds = 900;
var key = new RedisKey($"marketnest:cart:{userId}:reservation:{variantId}");
await db.StringSetAsync(key, value,
    expiry: TimeSpan.FromSeconds(ReservationTtlSeconds),
    when: When.NotExists); // atomic NX
```

**Use Lua script for atomic check-then-set:**
```csharp
// ✅ Atomic reservation via Lua
var script = LuaScript.Prepare(@"
    local current = redis.call('GET', @key)
    if current then
        redis.call('SET', @key, @value, 'EX', @ttl)
        return 1
    else
        return redis.call('SET', @key, @value, 'EX', @ttl, 'NX')
    end");
await db.ScriptEvaluateAsync(script, new { key, value, ttl = ReservationTtlSeconds });
```

---

### 2.6 EF Core Advanced Best Practices

```powershell
# Check SaveChanges interceptor for domain events
Select-String -Path src/**/*.cs -Pattern 'ISaveChangesInterceptor|SavingChangesAsync' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' } | Select-Object Path, Line

# Check connection resiliency
Select-String -Path src/**/*.cs -Pattern 'EnableRetryOnFailure|UseNpgsql' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' } | Select-Object Path, Line

# Check snake_case naming convention
Select-String -Path src/**/*.cs -Pattern 'UseSnakeCaseNamingConvention|EFCore\.NamingConventions' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' } | Select-Object Path, Line
```

```csharp
// ✅ Must have EnableRetryOnFailure + UseSnakeCaseNamingConvention
builder.Services.AddDbContext<CatalogDbContext>(opt =>
    opt.UseNpgsql(connectionString, npgsql =>
        npgsql.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null))
       .UseSnakeCaseNamingConvention()); // PostgreSQL snake_case convention

// ✅ Compiled query for hot path (product detail)
private static readonly Func<CatalogDbContext, Guid, Task<ProductDetailDto?>> GetProductById =
    EF.CompileAsyncQuery((CatalogDbContext db, Guid id) =>
        db.Products
          .AsNoTracking()
          .Where(p => p.Id == id && !p.IsDeleted)
          .Select(p => new ProductDetailDto(p.Id, p.Title, p.Description))
          .FirstOrDefault());
```

---

## Phase 3: REPORT — Database Review Report

```markdown
# Database Review Report — MarketNest
**Date**: <date>
**Scope**: EF Core migrations, PostgreSQL indexes, N+1 queries, Redis TTL, schema isolation

---

## Summary

| Category | Score (1–10) | Issues |
|---|---|---|
| Migration quality | X/10 | X findings |
| Schema-per-module isolation | X/10 | X findings |
| N+1 detection | X/10 | X findings |
| Index coverage | X/10 | X missing indexes |
| Redis key/TTL | X/10 | X findings |
| EF Core best practices | X/10 | X findings |

---

## 🔴 CRITICAL

1. **[Issue name]** — `path/to/file.cs:line`
   - Description: ...
   - Impact: Each request triggers N additional queries / full table scan / memory leak
   - Fix: ...

---

## 🟡 HIGH

...

---

## 🟢 MEDIUM / Quick wins

...

---

## Migrations to create

```csharp
// Migration: YYYYMMDD_HHmm_AddMissingIndexes.cs
migrationBuilder.CreateIndex(
    name: "IX_orders_buyer_id_status",
    schema: "orders",
    table: "orders",
    columns: new[] { "buyer_id", "status" });
```

---

## Redis key audit

| Key pattern | Current TTL | Correct TTL | Issue |
|---|---|---|---|
| marketnest:cart:* | persist | 900s | CRITICAL: memory leak |
```

---

## Phase 4: FIX — Apply Changes

**Mandatory rules:**
1. Confirm with the user before modifying entity configuration or creating new migrations
2. Create a new migration for each index/schema change — never edit existing migrations
3. Never edit `*.Designer.cs` files
4. After adding an index, the migration must have a matching `Down()` (`DropIndex`)
5. One migration = one clearly themed change

```powershell
# Create new migration for missing indexes
Push-Location src/MarketNest.Web
dotnet ef migrations add 20260425_1430_AddMissingIndexOrdersBuyer `
  --project ../MarketNest.Catalog `
  --startup-project .
Pop-Location

# Preview migration SQL before applying
dotnet ef migrations script --idempotent
```

---

## Phase 5: VERIFY — Post-fix Verification

```powershell
# Clean build
dotnet build MarketNest.slnx --no-incremental

# Apply migration to test DB
dotnet ef database update

# Run architecture tests (checks layer rules)
dotnet test tests/MarketNest.ArchitectureTests --no-build

# Run integration tests (Testcontainers auto-applies migrations)
dotnet test tests/MarketNest.IntegrationTests --no-build

# Verify indexes in PostgreSQL
$containerId = docker ps -q -f name=postgres
docker exec -it $containerId psql -U mn -d marketnest -c "
  SELECT schemaname, tablename, indexname, indexdef
  FROM pg_indexes
  WHERE schemaname IN ('catalog','orders','identity','payments','reviews','disputes')
  ORDER BY schemaname, tablename, indexname;"
```

---

## Quick Reference

| Problem | EF Core API |
|---|---|
| No tracking read | `.AsNoTracking()` |
| Cartesian explosion | `.AsSplitQuery()` |
| Hot path query | `EF.CompileAsyncQuery()` |
| Soft delete filter | `.HasQueryFilter(e => !e.IsDeleted)` |
| Money column | `.HasPrecision(18, 2)` |
| PostgreSQL snake_case | `UseSnakeCaseNamingConvention()` |
| Retry on failure | `EnableRetryOnFailure(3, 5s, null)` |
| Partial index | `.HasFilter("condition")` |
| GIN full-text index | `.HasMethod("GIN")` |
| Composite index | `HasIndex(e => new { e.A, e.B })` |

| Redis pattern | StackExchange.Redis API |
|---|---|
| Atomic set-if-not-exists | `StringSetAsync(key, val, ttl, When.NotExists)` |
| Atomic check-then-set | `ScriptEvaluateAsync(luaScript, ...)` |
| Renew TTL | `KeyExpireAsync(key, ttl)` |
| Key exists check | `KeyExistsAsync(key)` |
| Scan keys | `server.Keys(pattern: "marketnest:cart:*")` |
