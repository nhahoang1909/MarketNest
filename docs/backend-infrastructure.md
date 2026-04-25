# MarketNest — Backend Infrastructure & Advanced Patterns

> Version: 0.1 | Status: Draft | Date: 2026-04
> Consolidated from: `database-infrastructure-utilities.md` + `advanced-patterns-transaction-auth-fileupload.md`
> Covers: Query utilities, caching, event bus, transactions, UoW, `[Access]` permissions, file upload pipeline, testing utilities.

---

## Table of Contents

1. [Database Query Utilities](#1-database-query-utilities)
2. [Caching Infrastructure](#2-caching-infrastructure)
3. [Event Bus Abstraction](#3-event-bus-abstraction)
4. [Transaction Attribute & Write/Read Split](#4-transaction-attribute--writeread-split)
5. [Unit of Work — Domain Event Lifecycle](#5-unit-of-work--domain-event-lifecycle)
6. [Permission-Based Authorization (`[Access]`)](#6-permission-based-authorization-access)
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
public static class CacheKeys
{
    public static string Product(Guid id)       => $"catalog:product:{id}";
    public static string Storefront(string slug) => $"catalog:storefront:{slug}";
    public static string CartCount(Guid userId)  => $"cart:count:{userId}";
    public static string CommissionRate(Guid storeId) => $"payments:commission:{storeId}";

    public static class Ttl
    {
        public static readonly TimeSpan Short  = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan Medium = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan Long   = TimeSpan.FromMinutes(30);
    }
}
```

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
public interface IUnitOfWork
{
    IReadOnlyList<IDomainEvent> CollectPreCommitEvents();
    Task DispatchPostCommitEventsAsync(CancellationToken ct = default);
    Task<int> CommitAsync(CancellationToken ct = default);
}

// Pre-commit: IPreCommitEvent marker (runs inside TX)
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

## 6. Permission-Based Authorization (`[Access]`)

### Permission Enum

```csharp
public enum Permission
{
    Order_Read = 100, Order_Write = 101, Order_Cancel = 102, Order_Refund = 103,
    Product_Read = 200, Product_Write = 201, Product_Delete = 202,
    Storefront_Read = 300, Storefront_Write = 301, Storefront_Suspend = 302,
    Dispute_Read = 400, Dispute_Open = 401, Dispute_Respond = 402, Dispute_Arbitrate = 403,
    Review_Read = 500, Review_Write = 501, Review_Hide = 502,
    Payout_Read = 600, Payout_Process = 601,
    User_Read = 700, User_Suspend = 701, User_Delete = 702,
    Config_Read = 800, Config_Write = 801,
}
```

### `[Access]` Attribute & PermissionMatrix

```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class AccessAttribute(params Permission[] permissions) : Attribute
{
    public Permission[] Permissions { get; } = permissions;
    public bool RequireAll { get; init; } = false; // true = ALL required, false = ANY
}

// PermissionMatrix: single source of truth for role → permissions mapping
// Buyer, Seller, Admin roles with specific permission sets
// Admin has ALL permissions
```

### Usage

```csharp
[Access(Permission.Order_Write)]
[Transaction]
public async Task<IActionResult> PlaceOrder(...) { ... }

[Access(Permission.Dispute_Arbitrate)]  // Admin only
public async Task<IActionResult> ResolveDispute(...) { ... }

// Resource ownership: checked in handler via ICurrentUserService, not just permission attribute
```

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

