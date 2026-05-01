# MarketNest — Backend Infrastructure & Advanced Patterns

> Version: 0.1 | Status: Draft | Date: 2026-04
> Consolidated from: `database-infrastructure-utilities.md` + `advanced-patterns-transaction-auth-fileupload.md`
> Covers: Query utilities, caching, event bus, transactions, UoW, `[Access]` permissions, file upload pipeline, testing utilities.

---

## Table of Contents

1. [Database Query Utilities](#1-database-query-utilities) (incl. §1.4 `PgQueryBuilder` — raw SQL escape hatch)
2. [Caching Infrastructure](#2-caching-infrastructure)
3. [Event Bus Abstraction](#3-event-bus-abstraction)
4. [Transaction Attribute & Write/Read Split](#4-transaction-attribute--writeread-split)
5. [Unit of Work — Domain Event Lifecycle](#5-unit-of-work--domain-event-lifecycle)
6. [Permission-Based Authorization (`[Access]`) — ADR-044](#6-permission-based-authorization-access--adr-044)
7. [File Upload Pipeline](#7-file-upload-pipeline)
8. [HTTP Context & Request Utilities](#8-http-context--request-utilities)
9. [Error Handling Infrastructure](#9-error-handling-infrastructure)
10. [Testing Utilities](#10-testing-utilities)
11. [Registration Summary](#11-registration-summary)

---

## 1. Database Query Utilities

### 1.1 Dynamic Filter Extensions

```csharp
public static class QueryExtensions
{
    public static IQueryable<T> WhereIf<T>(this IQueryable<T> query, bool condition,
        Expression<Func<T, bool>> predicate) => condition ? query.Where(predicate) : query;

    public static IQueryable<T> WhereIfHasValue<T, TVal>(this IQueryable<T> query,
        TVal? value, Expression<Func<T, bool>> predicate) where TVal : struct
        => value.HasValue ? query.Where(predicate) : query;

    public static IQueryable<T> WhereILike<T>(this IQueryable<T> query,
        string? searchTerm, Expression<Func<T, string>> column) { /* PostgreSQL ILike */ }

    public static IQueryable<T> OrderByIf<T, TKey>(this IQueryable<T> query,
        bool descending, Expression<Func<T, TKey>> keySelector) => ...;
}

// Usage: much cleaner than nested if blocks
query = query
    .WhereILike(q.Search, p => p.Title)
    .WhereIfHasValue(q.Status,  p => p.Status == q.Status!.Value)
    .WhereIfHasValue(q.StoreId, p => p.StoreId == q.StoreId!.Value);
```

### 1.2 Specification Pattern

```csharp
public abstract class Specification<T>
{
    public abstract Expression<Func<T, bool>> ToExpression();
    public Specification<T> And(Specification<T> other) => new AndSpecification<T>(this, other);
    public Specification<T> Or(Specification<T> other)  => new OrSpecification<T>(this, other);
}

// Example: reusable across multiple handlers
public class ActiveOrdersForBuyerSpec(Guid buyerId) : Specification<Order>
{
    public override Expression<Func<Order, bool>> ToExpression()
        => o => o.BuyerId == buyerId && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled;
}
```

### 1.3 Bulk Operations

```csharp
public class BulkOperationService(MarketNestDbContext db)
{
    public Task BulkInsertAsync<T>(IList<T> entities, CancellationToken ct) where T : class => ...;
    public Task<int> BatchDeleteAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct) where T : class
        => db.Set<T>().Where(predicate).ExecuteDeleteAsync(ct);
    public Task<int> BatchUpdateAsync<T>(...) where T : class
        => db.Set<T>().Where(predicate).ExecuteUpdateAsync(setters, ct);
}
```

### 1.4 Raw PostgreSQL Query Builder (`PgQueryBuilder`)

> **Use only when EF Core cannot express the needed SQL** — e.g. complex multi-schema joins, DDL commands (`CREATE INDEX`, `CREATE SEQUENCE`, `CREATE SCHEMA`), PostgreSQL-specific features (`advisory locks`, `LISTEN/NOTIFY`), or bulk operations that bypass the Change Tracker.

**Location:** `Base.Infrastructure/Persistence/PgQueryBuilder.cs` — namespace `MarketNest.Base.Infrastructure`.

All value interpolation goes through positional parameters (`$1`, `$2`, …) to prevent SQL injection. Identifier quoting handles table/column names safely.

```csharp
// Interpolated query — values extracted as $1, $2, … automatically
var q = PgQueryBuilder.Query($"SELECT * FROM users WHERE id = {userId} AND active = {true}");
// q.Sql        => "SELECT * FROM users WHERE id = $1 AND active = $2"
// q.Parameters => [userId, true]

// Schema-qualified identifier quoting
PgQueryBuilder.Identifier("catalog", "variants")  // => "catalog"."variants"

// RawSqlFragment for trusted developer-controlled SQL (NEVER user input)
var sortCol = PgQueryBuilder.IdentifierRaw("created_at");
var q2 = PgQueryBuilder.Query($"SELECT * FROM orders ORDER BY {sortCol} DESC");

// LIKE pattern escaping
string safe = PgQueryBuilder.EscapeLike(userInput) + "%";
var q3 = PgQueryBuilder.Query($"SELECT * FROM products WHERE name LIKE {safe}");
```

**Available builders:** `Select`, `Insert`, `InsertMany`, `Update`, `Delete`, `Upsert`, `InClause`, `NotInClause`, `Combine` (re-indexes parameters across fragments), `EscapeLike`, `ToDebugString` (dev-only logging).

**Integration with Npgsql:**
```csharp
var q = PgQueryBuilder.Select("users", columns: ["id", "name"], 
    where: new Dictionary<string, object?> { ["role"] = "admin" }, schema: "identity");

await using var cmd = new NpgsqlCommand(q.Sql, conn);
for (int i = 0; i < q.Parameters.Count; i++)
    cmd.Parameters.AddWithValue($"p{i + 1}", q.Parameters[i] ?? DBNull.Value);
```

**Rules:**
- Prefer EF Core for all standard CRUD — `PgQueryBuilder` is the escape hatch, not the default.
- Never pass user input to `Raw()` or `IdentifierRaw()` — these bypass parameterization.
- `ToDebugString()` is for logging only — never execute its output.

---

## 2. Caching Infrastructure

### ICacheService

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default);
}
```

### Cache Key Constants

```csharp
// MarketNest.Base.Common/CacheKeys.cs — all keys centralized, no magic strings
public static class CacheKeys
{
    // Tier 1 — Reference Data (24h TTL)
    public static class ReferenceData   { Countries, Genders, PhoneCodes, Nationalities, Categories, ... }
    // Tier 2 — Business Config (1h TTL)
    public static class BusinessConfig  { OrderPolicy, CommissionDefault, StorefrontPolicy, ReviewPolicy, ... }
    // Module-specific keys
    public static class Catalog         { Product(id), ProductVariant(id), Storefront(slug), StorefrontById(id), ProductRating(id) }
    public static class Cart            { Count(userId) }
    public static class Payments        { CommissionRate(storeId) }
    public static class Identity        { UserPreferences(userId) }
    public static class Admin           { PlatformConfig(key), ProhibitedCategories }
    // TTL presets
    public static class Ttl             { VeryShort=30s, QuickExpiry=1m, Brief=5m, Medium=30m, BusinessConfig=1h, VeryLong=6h, ReferenceData=24h }
}
```

Full caching strategy: see `docs/caching-strategy.md`.

---

## 3. Event Bus Abstraction

```csharp
// Phase 1: wraps MediatR; Phase 3: wraps MassTransit (RabbitMQ)
public interface IDomainEventPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct) where TEvent : IDomainEvent;
}

// Phase 1
public class MediatRDomainEventPublisher(IPublisher publisher) : IDomainEventPublisher { ... }

// Phase 3
public class MassTransitDomainEventPublisher(IPublishEndpoint bus) : IDomainEventPublisher { ... }
```

---

## 4. Transaction Attribute & Write/Read Split

### Convention

```
Read (OnGet*):  No transaction, uses query handlers, cacheable
Write (OnPost*): Automatic transaction via RazorPageTransactionFilter
```

### `[Transaction]` Attribute

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class TransactionAttribute(
    IsolationLevel isolation = IsolationLevel.ReadCommitted,
    int timeoutSeconds = 30) : Attribute { ... }

// TransactionActionFilter wraps controller actions in BeginTransaction / Commit / Rollback
// RazorPageTransactionFilter auto-applies to all OnPost* handlers (opt out with [NoTransaction])
```

---

## 5. Unit of Work — Domain Event Lifecycle

```
1. Aggregate raises event → stored in DomainEvents list
2. UoW.CollectPreCommitEvents() → pulled for in-TX side effects
3. SaveChanges runs
4. UoW.DispatchPostCommitEventsAsync() → publishes externally (emails, queues)
```

```csharp
// Base.Infrastructure/Persistence/IUnitOfWork.cs
public interface IUnitOfWork : IAsyncDisposable
{
    // Transaction management (used by filters + background jobs)
    Task BeginTransactionAsync(IsolationLevel isolation = ReadCommitted, CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);

    // Event & persistence management
    IReadOnlyList<IDomainEvent> CollectPreCommitEvents();
    Task<int> CommitAsync(CancellationToken ct = default);  // SaveChanges + pre-commit events
    Task DispatchPostCommitEventsAsync(CancellationToken ct = default);
}

// NOTE: Command handlers DO NOT inject or call IUnitOfWork directly (ADR-027).
// The transaction filter (RazorPageTransactionFilter / TransactionActionFilter) manages
// the full lifecycle automatically. Only background jobs call IUnitOfWork explicitly.

// Pre-commit: IPreCommitDomainEvent marker (runs inside TX)
// Post-commit: all others (safe to fail, logged, Outbox in Phase 3)
```

### AggregateRoot

```csharp
public abstract class AggregateRoot : Entity<Guid>
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();
    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
```

---

## 6. Permission-Based Authorization (`[Access]`) — ADR-044

### Permission Enums — `[Flags]` per Module

Each module defines its own `[Flags] enum : long` in `Base.Common/Authorization/`. Permissions are bitwise-combined and stored as a single `bigint` per module per role.

```csharp
// Base.Common/Authorization/Permissions.cs

[Flags] public enum OrderPermission : long
{
    None = 0, View = 1 << 0, Edit = 1 << 1, Cancel = 1 << 2,
    Refund = 1 << 3, Export = 1 << 4,
    All = View | Edit | Cancel | Refund | Export
}

[Flags] public enum CatalogPermission : long
{
    None = 0, View = 1 << 8, Edit = 1 << 9, Delete = 1 << 10, Publish = 1 << 11,
    All = View | Edit | Delete | Publish
}

[Flags] public enum StorefrontPermission : long
{
    None = 0, View = 1 << 16, Edit = 1 << 17, Suspend = 1 << 18,
    All = View | Edit | Suspend
}

[Flags] public enum DisputePermission : long
{
    None = 0, View = 1 << 24, Open = 1 << 25, Respond = 1 << 26, Arbitrate = 1 << 27,
    All = View | Open | Respond | Arbitrate
}

[Flags] public enum ReviewPermission : long
{
    None = 0, View = 1 << 32, Write = 1 << 33, Hide = 1 << 34,
    All = View | Write | Hide
}

[Flags] public enum PaymentPermission : long
{
    None = 0, ViewPayout = 1 << 40, ProcessPayout = 1 << 41, Refund = 1 << 42,
    All = ViewPayout | ProcessPayout | Refund
}

[Flags] public enum UserPermission : long
{
    None = 0, View = 1 << 48, Suspend = 1 << 49, Delete = 1 << 50, Manage = 1 << 51,
    All = View | Suspend | Delete | Manage
}

[Flags] public enum ConfigPermission : long
{
    None = 0, View = 1 << 56, Write = 1 << 57,
    All = View | Write
}

[Flags] public enum PromotionPermission : long
{
    None = 0, View = 1 << 60, Create = 1 << 61, Pause = 1 << 62,
    All = View | Create | Pause
}

public enum PermissionModule
{
    Order = 1, Catalog = 2, Storefront = 3, Dispute = 4, Review = 5,
    Payment = 6, User = 7, Config = 8, Promotion = 9,
}
```

### `[Access]` Attribute — Typed Per-Module

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class AccessAttribute : Attribute
{
    // One constructor per permission enum
    public AccessAttribute(OrderPermission order) { ... }
    public AccessAttribute(CatalogPermission catalog) { ... }
    public AccessAttribute(DisputePermission dispute) { ... }
    public AccessAttribute(UserPermission user) { ... }
    public AccessAttribute(ConfigPermission config) { ... }
    // ... etc.
    internal PermissionRequirement Requirement { get; }
}
```

### Usage

```csharp
[Access(OrderPermission.View)]
public async Task<IActionResult> OnGetAsync(...) { }

[Access(OrderPermission.Edit | OrderPermission.Cancel)]   // user needs BOTH flags
public async Task<IActionResult> OnPostCancelAsync(...) { }

[Access(DisputePermission.Arbitrate)]   // Administrator only
public async Task<IActionResult> OnPostArbitrateAsync(...) { }

// Multiple [Access] attributes on the same handler are ANDed across modules
[Access(UserPermission.Manage)]
[Access(ConfigPermission.Write)]
public async Task<IActionResult> OnPostPromoteToAdmin(...) { }
```

### Permission Resolution at Login

```
1. Load User with Roles (include RolePermissions + UserPermissionOverrides)
2. For each PermissionModule M:
     effective[M] = 0
     foreach role in user.Roles: effective[M] |= role.GetFlags(M)
     override = user.Overrides.Find(M, not expired)
     if override: effective[M] = (effective[M] | override.Granted) & ~override.Denied
3. Emit JWT claim "mn.perm.{module}" = effective[M].ToString() (only if non-zero)
```

### JWT Claim Layout

```
Standard:  sub, email, name, role (array: ["Buyer", "Seller"])
Custom:    mn.store = storefrontId (Sellers only)
Perms:     mn.perm.order = "23", mn.perm.catalog = "3840", ...
Security:  jti, iat, exp (15 min)
```

### AccessFilter (IAsyncAuthorizationFilter)

```
Request → AuthN middleware → RuntimeContext (builds ICurrentUser with perm claims)
       → AccessFilter: reads [Access] attrs, bitwise-checks each against ICurrentUser
       → 401 if not authenticated, 403 if permission denied
       → Handler: horizontal ownership check inline
```

### Horizontal Authorization (in handlers)

```csharp
// Ownership check pattern — admin bypasses
if (!currentUser.IsAdmin)
{
    if (order.BuyerId != currentUser.RequireId() && order.StoreId != currentUser.SellerStoreId)
        return Error.Forbidden("You do not have access to this order");
}
```

### Admin Permission Override

Admin can add/remove per-user permissions via `PUT /admin/users/{id}/permissions`:
- `SetUserPermissionOverrideCommand(UserId, Module, GrantedFlags, DeniedFlags, AdminId)`
- Effect takes place on user's next token issuance (login or refresh)
- Optional `ExpiresAt` for temporary grants

---

## 7. File Upload Pipeline

### Storage Strategy by Phase

| Phase | Dev | Production |
|-------|-----|-----------|
| 1 | Local disk (`wwwroot/uploads/`) | Local disk or MinIO |
| 2+ | MinIO in Docker Compose | Cloudflare R2 (S3-compatible, free tier) |
| 4 (K8s) | MinIO sidecar | Cloudflare R2 or SeaweedFS |

### Upload Pipeline

```
IFormFile[] → 1. Validate (MIME, size, count)
            → 2. Antivirus scan (ClamAV, skip in dev)
            → 3. Image optimization (resize, WebP)
            → 4. Upload to IStorageService
            → 5. Save UploadedFile record to DB
            → Return { fileId, url, thumbnailUrl }
```

### File Upload Rules

```csharp
public static class FileUploadRules
{
    public static readonly AllowedFileTypes ProductImages = new(MaxCount: 5, MaxSizeBytes: 5MB, ...);
    public static readonly AllowedFileTypes StorefrontBanner = new(MaxCount: 1, MaxSizeBytes: 2MB, ...);
    public static readonly AllowedFileTypes DisputeEvidence = new(MaxCount: 5, MaxSizeBytes: 10MB, incl PDF);
}
```

### UploadedFile Entity

```csharp
// Domain entities store FileId (Guid), never raw URLs.
// URL resolved at query time → allows CDN migration without DB updates.
public class UploadedFile : Entity<Guid>, IAuditable
{
    public string StorageKey { get; private set; }
    public string OriginalFileName { get; private set; }
    public string ContentType { get; private set; }
    public long SizeBytes { get; private set; }
    public FileContext Context { get; private set; }    // ProductImage, DisputeEvidence, etc.
    public bool IsOrphaned { get; private set; }        // not linked to entity yet
    public DateTime ExpiresAt { get; private set; }     // orphan cleanup after 1hr
}
```

### Orphan File Cleanup Job

```csharp
// Runs hourly, deletes UploadedFiles that were never claimed (IsOrphaned=true, expired)
public class OrphanFileCleanupJob : IBackgroundJob { ... }
```

---

## 8. HTTP Context & Request Utilities

```csharp
// HttpRequest extensions
public static bool IsHtmx(this HttpRequest request) => request.Headers.ContainsKey("HX-Request");
public static bool IsHtmxPartial(this HttpRequest request) => IsHtmx() && !IsHtmxBoosted();
public static string GetClientIp(this HttpRequest request) => /* X-Forwarded-For aware */;

// ClaimsPrincipal extensions
public static Guid GetUserId(this ClaimsPrincipal user) => ...;
public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole("Admin");
public static bool IsSeller(this ClaimsPrincipal user) => user.IsInRole("Seller");
```

### Correlation ID Middleware

```csharp
// Reads X-Correlation-ID from request (or generates 8-char ID)
// Sets on response, pushes to Serilog LogContext + Activity tags
```

---

## 9. Error Handling Infrastructure

```csharp
// ExceptionMiddleware maps exceptions to Problem Details (RFC 7807):
// ValidationException → 422, NotFoundException → 404,
// UnauthorizedException → 401, ForbiddenException → 403
// Unknown → 500 with CorrelationId
```

---

## 10. Testing Utilities

### MarketNestWebAppFactory

```csharp
public class MarketNestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Spins up PostgreSQL 16 + Redis 7 via Testcontainers
    // Overrides connection strings, email (no-op), time (deterministic)
}
```

### Test Data Builders

```csharp
// Fluent builders for valid domain objects
public class OrderBuilder
{
    public OrderBuilder WithBuyer(Guid id) { ... }
    public OrderBuilder WithUnitPrice(decimal amount) { ... }
    public Order Build() { ... }
    public Order BuildConfirmed() { ... }
    public Order BuildShipped() { ... }
}
```

### Integration Test Base

```csharp
public abstract class IntegrationTestBase(MarketNestWebAppFactory factory)
    : IClassFixture<MarketNestWebAppFactory>
{
    protected async Task<HttpClient> AsAdminAsync()  => ...;
    protected async Task<HttpClient> AsSellerAsync() => ...;
    protected async Task<HttpClient> AsBuyerAsync()  => ...;
    protected async Task<T> DbQueryAsync<T>(...) => ...;
}
```

---

## 11. Registration Summary

```csharp
builder.Services
    // Filters
    .AddControllers(opt => { opt.Filters.Add<TransactionActionFilter>(); opt.Filters.Add<AccessFilter>(); })
    // Unit of Work
    .AddScoped<IUnitOfWork, UnitOfWork>()
    // File services
    .AddScoped<IFileUploadService, FileUploadService>()
    .AddScoped<IAntivirusScanner, ClamAvScanner>()
    .AddScoped<IImageProcessor, SkiaSharpImageProcessor>()
    .AddStorageService(builder.Configuration)   // "local" | "s3"
    // Authorization
    .AddScoped<IAuthorizationHandler, AccessAuthorizationHandler>()
    // Jobs
    .AddScoped<IBackgroundJob, OrphanFileCleanupJob>();
```

