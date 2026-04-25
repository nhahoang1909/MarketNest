---
name: performance-optimizer
description: >
  Scan the entire project directory, analyze code at a granular level, and optimize performance
  per language/framework. Use this skill when the user wants to: optimize code performance, review
  bottlenecks, apply performance best practices, analyze algorithmic complexity, find memory leaks,
  improve render speed (frontend), optimize database queries, reduce bundle size, or says anything
  like "optimize code", "improve performance", "slow code", "review performance", "find bottleneck",
  "improve speed", "profiling", "refactor for performance". Activate even when the user simply says
  "my code is slow" or "make the app faster".
compatibility:
  tools: [bash, read_file, write_file, list_files, grep_search, run_in_terminal]
  agents: [claude-code, gemini-cli, cursor, continue, aider, copilot]
---

# Performance Optimizer Skill

This skill guides the agent to scan the entire codebase, identify performance weaknesses, and apply best practices to improve speed, memory usage, and scalability.

> **Target project**: MarketNest — .NET 10 Modular Monolith, Razor Pages + HTMX + Alpine.js, PostgreSQL, Redis, RabbitMQ.
> Always read `CLAUDE.md` and `AGENTS.md` at the repo root to understand conventions before modifying code.

---

## Execution Flow (Mandatory order)

```
Phase 1: SCAN      → Scan directory structure & collect metadata
Phase 2: ANALYZE   → Read each file, detect anti-patterns
Phase 3: REPORT    → Compile report prioritized by impact
Phase 4: FIX       → Apply changes (if permitted)
Phase 5: VERIFY    → Validate after changes
```

---

## Phase 1: SCAN — Scan Directory Structure

### Step 1.1 — Identify languages & frameworks

**On Windows (PowerShell):**
```powershell
# List all files (exclude bin, obj, node_modules, .git)
Get-ChildItem -Recurse -File -Exclude *.dll,*.exe |
  Where-Object { $_.FullName -notmatch '\\(bin|obj|node_modules|\.git|dist|wwwroot\\lib)\\' } |
  Select-Object FullName | Sort-Object FullName

# Count by extension
Get-ChildItem -Recurse -File |
  Where-Object { $_.FullName -notmatch '\\(bin|obj|node_modules|\.git)\\' } |
  Group-Object Extension | Sort-Object Count -Descending | Select-Object -First 20 Count, Name
```

**On Linux/macOS (Bash):**
```bash
find . -type f \
  -not -path "*/bin/*" -not -path "*/obj/*" \
  -not -path "*/node_modules/*" -not -path "*/.git/*" \
  | sed 's/.*\.//' | sort | uniq -c | sort -rn | head -20
```

Identify:
- **Primary languages**: C# (.cs), Razor (.cshtml), TypeScript/JavaScript (.ts/.js)
- **Frameworks**: ASP.NET Core, EF Core, MediatR (CQRS), Razor Pages, HTMX, Alpine.js
- **Build tools**: dotnet CLI, npm (Tailwind CSS)
- **Database**: PostgreSQL 16 (EF Core), Redis (caching)

### Step 1.2 — Collect size metadata

```powershell
# Top 20 largest C# files
Get-ChildItem -Recurse -Include *.cs,*.cshtml |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  Sort-Object Length -Descending | Select-Object -First 20 Length, FullName

# Check wwwroot size (static assets)
Get-ChildItem -Recurse src/MarketNest.Web/wwwroot | Measure-Object -Property Length -Sum
```

---

## Phase 2: ANALYZE — Detailed Code Analysis

Read each **important** file (prioritize large files, core files, and files with the most injections/references).

### 2A. Anti-patterns — C# / .NET / EF Core

| Anti-pattern | Signs to look for | Fix |
|---|---|---|
| N+1 Query (EF Core) | Navigation property accessed in loop without `.Include()` | Use `.Include()` / `.ThenInclude()`, or projection `.Select()` |
| Unnecessary tracking query | Read-only query missing `.AsNoTracking()` | Add `.AsNoTracking()` for read-only queries |
| Premature `ToListAsync()` | `.ToList()` before `.Where()` / `.Select()` → loads entire table into memory | Push filter/projection before materialization |
| Blocking async (sync-over-async) | `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` on Task | Use `await` properly, async all the way |
| String concat in loop | `+=` with string in loop | `StringBuilder` or `string.Join()` |
| Missing `CancellationToken` | Async method not propagating `CancellationToken` | Propagate `CancellationToken` through all async calls |
| Boxing value type | `object` receiving value type, `Equals()` on struct | Generic constraint, `IEquatable<T>` |
| Allocation in hot path | `new List<>()` / `new Dictionary<>()` per request | Object pooling, `ArrayPool<T>`, stackalloc |
| Catch generic Exception | `catch (Exception ex)` swallowing everything | Catch specific exceptions, use `Result<T,Error>` pattern |
| IEnumerable multiple enumeration | `IEnumerable<T>` param iterated multiple times | Materialize to `List<T>` or `T[]` first |

```powershell
# Find sync-over-async
Select-String -Path src/**/*.cs -Pattern '\.Result\b|\.Wait\(\)|\.GetAwaiter\(\)\.GetResult\(\)' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Find missing AsNoTracking
Select-String -Path src/**/*.cs -Pattern '\.ToListAsync|\.FirstOrDefaultAsync|\.SingleAsync' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }
# Then check whether AsNoTracking() is on the same query

# Find N+1 pattern: navigation property in foreach
Select-String -Path src/**/*.cs -Pattern 'foreach.*\.' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Find string concat in loop
Select-String -Path src/**/*.cs -Pattern '\+=' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Find missing CancellationToken in async methods
Select-String -Path src/**/*.cs -Pattern 'async Task' -Recurse |
  Where-Object { $_.Line -notmatch 'CancellationToken' -and $_.Path -notmatch '\\(bin|obj)\\' }
```

### 2B. Anti-patterns — Razor Pages / HTMX / Alpine.js / Frontend

| Anti-pattern | Signs | Fix |
|---|---|---|
| Inline `<script>` blocking render | `<script>` missing `defer` or `async` | Add `defer` to all scripts |
| Images missing lazy loading | `<img>` missing `loading="lazy"` | Add `loading="lazy"` for below-the-fold images |
| CSS not purged | Tailwind output too large with unused classes | Ensure Tailwind content config is correct |
| Heavy Alpine.js `x-data` | Large object literal, complex inline computed | Extract to Alpine component `Alpine.data()` |
| HTMX full page swap | `hx-target="body"` instead of partial swap | Swap only the needed section `hx-target="#specific-id"` |
| Unpinned CDN dependency | `@latest` or `@3.x.x` — may break unexpectedly | Pin exact version: `@3.14.1` |
| Missing `asp-append-version` | Static file missing cache busting | Add `asp-append-version="true"` |
| Render-blocking font | Google Fonts missing `display=swap` | Add `&display=swap`, preconnect hints |

```powershell
# Find images missing lazy loading
Select-String -Path src/**/*.cshtml -Pattern '<img' -Recurse |
  Where-Object { $_.Line -notmatch 'loading=' }

# Find scripts missing defer/async
Select-String -Path src/**/*.cshtml -Pattern '<script' -Recurse |
  Where-Object { $_.Line -notmatch 'defer|async' -and $_.Line -notmatch '/script>' }

# Find CDN without pinned version
Select-String -Path src/**/*.cshtml -Pattern 'cdn\.jsdelivr|unpkg|cdnjs' -Recurse |
  Where-Object { $_.Line -match '@\d+\.x|@latest|@@\d+\.x' }

# CSS output size
Get-Item src/MarketNest.Web/wwwroot/css/site.css -ErrorAction SilentlyContinue |
  Select-Object Length, FullName
```

### 2C. Database & EF Core Queries

```powershell
# Find raw SQL
Select-String -Path src/**/*.cs -Pattern 'FromSqlRaw|ExecuteSqlRaw|FromSqlInterpolated' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Find SaveChanges in loops
Select-String -Path src/**/*.cs -Pattern 'SaveChanges' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# Find missing index hints — check migration files
Get-ChildItem -Recurse -Include *Migration*.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-Object FullName
```

Checklist:
- [ ] Queries inside loops → batch query / `.Include()`
- [ ] Missing index on frequently filtered/joined columns
- [ ] `SELECT *` (implicit) → projection `.Select()` fetching only needed columns
- [ ] Missing pagination on list endpoints (`.Skip().Take()`)
- [ ] `SaveChangesAsync()` called multiple times → consolidate to one call at end of unit of work
- [ ] Connection pool size appropriate (`MaxPoolSize` in connection string)

### 2D. Caching (Redis / In-Memory)

```powershell
# Find Redis / IDistributedCache / IMemoryCache usage
Select-String -Path src/**/*.cs -Pattern 'IDistributedCache|IMemoryCache|StackExchange\.Redis|ConnectionMultiplexer' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }
```

Checklist:
- [ ] Is hot data (category lists, config) being cached?
- [ ] Is cache invalidation strategy clear (event-driven or TTL)?
- [ ] Is cache stampede protection in place (lock/semaphore)?
- [ ] Is Redis key naming convention consistent?

### 2E. Middleware & Request Pipeline

```powershell
# Check Program.cs — middleware order
Get-Content src/MarketNest.Web/Program.cs | Select-String -Pattern 'app\.Use|app\.Map'
```

Checklist:
- [ ] Response compression (Brotli/Gzip) enabled?
- [ ] Static file middleware placed before routing?
- [ ] Health check endpoint lightweight, no DB queries?
- [ ] Rate limiting on public API endpoints?

---

## Phase 3: REPORT — Analysis Report

Generate a report in the following format:

```markdown
# Performance Audit Report
**Project**: MarketNest
**Date**: <date>
**Stack**: .NET 10, EF Core 10, PostgreSQL 16, Redis, Razor Pages + HTMX + Alpine.js

## Score Summary (1–10)
| Category | Score | Brief Notes |
|---|---|---|
| EF Core Query Efficiency | X/10 | ... |
| Async/Await Correctness | X/10 | ... |
| Memory & Allocation | X/10 | ... |
| Caching Strategy | X/10 | ... |
| Frontend Performance | X/10 | ... |
| Database Indexing | X/10 | ... |
| Middleware Pipeline | X/10 | ... |

## Issues by Priority

### 🔴 CRITICAL (High impact, fix immediately)
1. **[Issue name]** — `path/to/file.cs:line`
   - Description: ...
   - Estimated impact: ~X% latency / X MB memory
   - Suggested fix: ...

### 🟡 HIGH (Fix this sprint)
...

### 🟢 MEDIUM (Backlog)
...

### 💡 QUICK WINS (< 30 minutes each)
...

## Suggested Changes
- [ ] File A: ...
- [ ] File B: ...
```

---

## Phase 4: FIX — Apply Changes

**Mandatory rules before making changes:**
1. **Always ask for confirmation** before editing a file (unless the user said "just fix it")
2. **Edit one file at a time**, do not batch-edit multiple files simultaneously
3. **Preserve business logic**, only change implementation for optimization
4. **Document before/after** for each change
5. Do not add new features, do not refactor unrelated structure
6. **Follow project code conventions** — read `CLAUDE.md` / `AGENTS.md` / `docs/code-rules.md`
7. **Use `Result<T, Error>`** — do not throw exceptions for business logic
8. **Flat namespaces** — do not add sub-folders to namespaces

### Template for Each Change

```
📁 File: src/MarketNest.Catalog/Infrastructure/Queries/GetProductsQueryHandler.cs
🔴 Issue: Missing AsNoTracking() for read-only query
📊 Impact: ~15% fewer allocations, reduced EF change tracker overhead

BEFORE:
```csharp
var products = await _context.Products
    .Where(p => p.IsActive)
    .ToListAsync(cancellationToken);
```

AFTER:
```csharp
var products = await _context.Products
    .AsNoTracking()
    .Where(p => p.IsActive)
    .ToListAsync(cancellationToken);
```

✅ Reason: Query handler only reads data, change tracking is unnecessary
```

---

## Phase 5: VERIFY — Post-Change Validation

```powershell
# Build project
dotnet build

# Run tests
dotnet test

# Check compile/lint errors
dotnet build --no-restore 2>&1 | Select-String -Pattern 'error|warning'

# Check CSS size
Get-Item src/MarketNest.Web/wwwroot/css/site.css | Select-Object Length
```

Report back:
- ✅/❌ Build succeeded
- ✅/❌ All tests passed
- 📊 Before vs. after metrics comparison (if measurable)
- ⚠️ Any regressions introduced

---

## Best Practices by Area (MarketNest-specific)

### EF Core Query Optimization
```csharp
// ❌ N+1: Load products then loop-load category
var products = await _context.Products.ToListAsync();
foreach (var p in products)
    Console.WriteLine(p.Category.Name); // Lazy load each iteration

// ✅ Eager load
var products = await _context.Products
    .AsNoTracking()
    .Include(p => p.Category)
    .ToListAsync();

// ✅ Projection (best — fetch only needed columns)
var dtos = await _context.Products
    .AsNoTracking()
    .Select(p => new ProductDto(p.Id, p.Name, p.Category.Name))
    .ToListAsync();
```

### Async All The Way
```csharp
// ❌ Sync-over-async — deadlock risk
var result = _service.GetDataAsync().Result;

// ✅ Proper async
var result = await _service.GetDataAsync(cancellationToken);
```

### Caching Strategy (Redis)
```csharp
// Cache hot data: categories, platform config
// Key format: "mn:{module}:{entity}:{id}" — e.g. "mn:catalog:categories:all"
// TTL: 5–15 minutes for lists, 1 hour for config
// Invalidation: domain event → clear cache
```

### Database Index Checklist
- [ ] All foreign keys have indexes (EF Core creates them automatically, but verify)
- [ ] Frequently filtered columns: `IsActive`, `Status`, `CreatedAt`, `SlugUrl`
- [ ] Composite index for common multi-column queries
- [ ] Partial index for soft-delete: `WHERE "IsDeleted" = false`
- [ ] GIN index for full-text search (if applicable)

### Frontend Performance
```html
<!-- ✅ Lazy load below-the-fold images -->
<img src="..." loading="lazy" decoding="async" alt="..." />

<!-- ✅ Preconnect for CDN fonts -->
<link rel="preconnect" href="https://fonts.googleapis.com" />
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />

<!-- ✅ Pin CDN version -->
<script src="https://unpkg.com/htmx.org@2.0.4" defer></script>

<!-- ✅ HTMX partial swap instead of full page -->
<div hx-get="/catalog/products?page=2"
     hx-target="#product-grid"
     hx-swap="innerHTML">
  Load more
</div>
```

### Middleware Pipeline Order
```csharp
// Optimal order in Program.cs:
app.UseResponseCompression();   // 1. Compress as early as possible
app.UseStaticFiles();           // 2. Serve static before routing
app.UseRouting();               // 3. Routing
app.UseAuthentication();        // 4. Auth
app.UseAuthorization();         // 5. Authz
app.UseRateLimiter();           // 6. Rate limit
app.MapRazorPages();            // 7. Endpoints
app.MapHealthChecks("/health"); // 8. Health
```

---

## Important Notes

- **Measure first, optimize later**: Do not optimize what has not been measured as slow
- **Premature optimization is the root of all evil**: Only optimize actual hot paths
- **Read thoroughly before editing**: Understand context before changing any line of code
- **Follow project conventions**: Read `CLAUDE.md`, `AGENTS.md`, `docs/code-rules.md` before editing
- **Use `Result<T, Error>`**: Do not throw exceptions for business failures
- **Every change must have a specific justification**, do not edit just to "make it look nicer"
- **Log bugs/decisions** in `docs/project_notes/` if important issues are discovered
