# MarketNest — Database & Infrastructure Utilities

> Version: 0.1 | Status: Draft | Date: 2026-04  
> Common DB utilities, query helpers, background job infrastructure, and integration patterns.

---

## 1. Database Query Utilities

### 1.1 IQueryBuilder — Dynamic Filter Builder

Avoid raw string concatenation for dynamic queries. Use a composable builder:

```csharp
// Core/Common/Persistence/QueryBuilder.cs
/// <summary>
/// Fluent builder for dynamic EF Core queries.
/// Eliminates repetitive if (!string.IsNullOrEmpty) { query = query.Where(...) } patterns.
/// </summary>
public static class QueryExtensions
{
    /// <summary>Apply condition only if predicate is true</summary>
    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> query,
        bool condition,
        Expression<Func<T, bool>> predicate)
        => condition ? query.Where(predicate) : query;

    /// <summary>Apply condition only if value is not null/empty</summary>
    public static IQueryable<T> WhereIfNotNull<T, TVal>(
        this IQueryable<T> query,
        TVal? value,
        Expression<Func<T, bool>> predicate)
        where TVal : class
        => value is not null ? query.Where(predicate) : query;

    public static IQueryable<T> WhereIfHasValue<T, TVal>(
        this IQueryable<T> query,
        TVal? value,
        Expression<Func<T, bool>> predicate)
        where TVal : struct
        => value.HasValue ? query.Where(predicate) : query;

    /// <summary>Full-text search using PostgreSQL ILike</summary>
    public static IQueryable<T> WhereILike<T>(
        this IQueryable<T> query,
        string? searchTerm,
        Expression<Func<T, string>> column)
    {
        if (string.IsNullOrWhiteSpace(searchTerm)) return query;
        var pattern = $"%{searchTerm.Trim()}%";
        // Build expression: EF.Functions.ILike(column, pattern)
        var param = column.Parameters[0];
        var body = Expression.Call(
            typeof(NpgsqlDbFunctionsExtensions),
            nameof(NpgsqlDbFunctionsExtensions.ILike),
            null,
            Expression.Constant(EF.Functions),
            column.Body,
            Expression.Constant(pattern));
        return query.Where(Expression.Lambda<Func<T, bool>>(body, param));
    }

    /// <summary>Conditional OrderBy / OrderByDescending</summary>
    public static IQueryable<T> OrderByIf<T, TKey>(
        this IQueryable<T> query,
        bool descending,
        Expression<Func<T, TKey>> keySelector)
        => descending
            ? query.OrderByDescending(keySelector)
            : query.OrderBy(keySelector);
}
```

**Usage — much cleaner:**
```csharp
// Before:
if (!string.IsNullOrEmpty(q.Search))
    query = query.Where(p => EF.Functions.ILike(p.Title, $"%{q.Search}%"));
if (q.Status.HasValue)
    query = query.Where(p => p.Status == q.Status);
if (q.StoreId.HasValue)
    query = query.Where(p => p.StoreId == q.StoreId);

// After:
query = query
    .WhereILike(q.Search, p => p.Title)
    .WhereIfHasValue(q.Status,  p => p.Status == q.Status!.Value)
    .WhereIfHasValue(q.StoreId, p => p.StoreId == q.StoreId!.Value);
```

---

### 1.2 Specification Pattern (for complex queries)

For queries reused across multiple handlers, extract into a Specification:

```csharp
// Core/Common/Persistence/Specification.cs
public abstract class Specification<T>
{
    public abstract Expression<Func<T, bool>> ToExpression();

    public bool IsSatisfiedBy(T entity)
        => ToExpression().Compile()(entity);

    public Specification<T> And(Specification<T> other)
        => new AndSpecification<T>(this, other);

    public Specification<T> Or(Specification<T> other)
        => new OrSpecification<T>(this, other);
}

public static class SpecificationExtensions
{
    public static IQueryable<T> Where<T>(this IQueryable<T> query, Specification<T> spec)
        => query.Where(spec.ToExpression());
}
```

```csharp
// Example: reused in multiple handlers
// Orders/Application/Specifications/ActiveOrdersForBuyerSpec.cs
public class ActiveOrdersForBuyerSpec(Guid buyerId) : Specification<Order>
{
    public override Expression<Func<Order, bool>> ToExpression()
        => o => o.BuyerId == buyerId
             && o.Status != OrderStatus.Completed
             && o.Status != OrderStatus.Cancelled;
}

// Usage
var orders = await db.Orders
    .Where(new ActiveOrdersForBuyerSpec(buyerId))
    .AsNoTracking()
    .ToListAsync(ct);
```

---

### 1.3 Bulk Operations Helper

```csharp
// Infrastructure/Persistence/BulkOperationService.cs
/// <summary>
/// For batch updates/inserts — uses EFCore.BulkExtensions.
/// Never loop SaveChanges for bulk data.
/// </summary>
public class BulkOperationService(MarketNestDbContext db)
{
    /// <summary>Bulk insert without change tracking overhead</summary>
    public Task BulkInsertAsync<T>(IList<T> entities, CancellationToken ct = default)
        where T : class
        => db.BulkInsertAsync(entities, cancellationToken: ct);

    /// <summary>Bulk update specific columns only</summary>
    public Task BulkUpdateAsync<T>(IList<T> entities, BulkConfig? config = null, CancellationToken ct = default)
        where T : class
        => db.BulkUpdateAsync(entities, config, cancellationToken: ct);

    /// <summary>Batch delete via predicate (no entity materialization)</summary>
    public Task<int> BatchDeleteAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        where T : class
        => db.Set<T>().Where(predicate).ExecuteDeleteAsync(ct);

    /// <summary>Batch update via predicate (EF 7+ ExecuteUpdate)</summary>
    public Task<int> BatchUpdateAsync<T>(
        Expression<Func<T, bool>> predicate,
        Expression<Func<SetPropertyCalls<T>, SetPropertyCalls<T>>> setters,
        CancellationToken ct = default)
        where T : class
        => db.Set<T>().Where(predicate).ExecuteUpdateAsync(setters, ct);
}
```

```csharp
// Example: batch release expired cart reservations
await bulk.BatchUpdateAsync<InventoryItem>(
    predicate: i => expiredVariantIds.Contains(i.VariantId),
    setters: s => s.SetProperty(i => i.QuantityReserved, 0));
```

---

## 2. Background Job Infrastructure

### 2.1 IBackgroundJob Contract

```csharp
// Core/Common/Jobs/IBackgroundJob.cs
public interface IBackgroundJob
{
    string JobId   { get; }           // Unique ID for Hangfire/Quartz registration
    string CronExpression { get; }    // "*/5 * * * *" = every 5 min
    bool   RunOnStartup   { get; }    // Run immediately on app start (useful for cleanup)
    
    Task ExecuteAsync(CancellationToken ct = default);
}
```

### 2.2 Job Implementations

```csharp
// Cart/Application/Jobs/CleanupExpiredReservationsJob.cs
public class CleanupExpiredReservationsJob(
    MarketNestDbContext db,
    ILogger<CleanupExpiredReservationsJob> logger) : IBackgroundJob
{
    public string JobId          => "cart.cleanup-reservations";
    public string CronExpression => "*/5 * * * *"; // every 5 min
    public bool   RunOnStartup   => true;

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        // Release DB reservations where Redis key has expired (older than 20 min)
        var cutoff = DateTime.UtcNow.AddMinutes(-20);
        
        var affected = await db.InventoryItems
            .Where(i => i.QuantityReserved > 0 && i.LastReservedAt < cutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.QuantityReserved, 0)
                .SetProperty(i => i.LastReservedAt, (DateTime?)null), ct);

        if (affected > 0)
            logger.LogInformation("Released {Count} expired cart reservations", affected);
    }
}

// Orders/Application/Jobs/AutoCancelUnconfirmedOrdersJob.cs
public class AutoCancelUnconfirmedOrdersJob(
    IOrderRepository orders,
    IPublisher publisher,
    IDateTimeService clock,
    ILogger<AutoCancelUnconfirmedOrdersJob> logger) : IBackgroundJob
{
    public string JobId          => "orders.auto-cancel";
    public string CronExpression => "*/30 * * * *"; // every 30 min
    public bool   RunOnStartup   => false;

    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        var cutoff = clock.UtcNow.AddHours(-48);
        var staleOrders = await orders.GetConfirmedOlderThanAsync(cutoff, ct);

        foreach (var order in staleOrders)
        {
            var result = order.Cancel("Seller did not confirm within 48 hours");
            if (result.IsSuccess)
            {
                orders.Update(order);
                logger.LogInformation("Auto-cancelled order {OrderId}", order.Id);
            }
        }

        if (staleOrders.Any())
            await orders.SaveChangesAsync(ct);
    }
}
```

### 2.3 Job Registration (Hangfire)

```csharp
// Infrastructure/Jobs/JobRegistrar.cs
public static class JobRegistrar
{
    public static void RegisterJobs(IServiceProvider services)
    {
        var jobs = services.GetServices<IBackgroundJob>();

        foreach (var job in jobs)
        {
            RecurringJob.AddOrUpdate(
                recurringJobId: job.JobId,
                methodCall: () => RunJob(job.JobId, services),
                cronExpression: job.CronExpression,
                options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

            if (job.RunOnStartup)
                BackgroundJob.Enqueue(() => RunJob(job.JobId, services));
        }
    }

    public static async Task RunJob(string jobId, IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var job = scope.ServiceProvider
            .GetServices<IBackgroundJob>()
            .First(j => j.JobId == jobId);
        await job.ExecuteAsync();
    }
}

// Each module registers its jobs:
services.AddScoped<IBackgroundJob, CleanupExpiredReservationsJob>();
services.AddScoped<IBackgroundJob, AutoCancelUnconfirmedOrdersJob>();
services.AddScoped<IBackgroundJob, AutoCompleteDeliveredOrdersJob>();
services.AddScoped<IBackgroundJob, ProcessPayoutBatchJob>();
```

---

## 3. Caching Infrastructure

### 3.1 ICacheService — Unified Cache Abstraction

```csharp
// Core/Common/Caching/ICacheService.cs
/// <summary>
/// Unified abstraction over Redis (prod) / MemoryCache (dev/test).
/// Never inject IDistributedCache or IMemoryCache directly — always use this.
/// </summary>
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    
    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken ct = default);

    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);

    /// <summary>Get from cache or execute factory and store result</summary>
    Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default);
}
```

### 3.2 Cache Key Constants

```csharp
// Core/Common/Caching/CacheKeys.cs
public static class CacheKeys
{
    // Template: {module}:{entity}:{id}
    public static string Product(Guid id)       => $"catalog:product:{id}";
    public static string ProductList(string key) => $"catalog:products:{key}";
    public static string Storefront(string slug) => $"catalog:storefront:{slug}";
    public static string CartCount(Guid userId)  => $"cart:count:{userId}";
    public static string UserRole(Guid userId)   => $"identity:role:{userId}";
    public static string CommissionRate(Guid storeId) => $"payments:commission:{storeId}";

    // Cache durations
    public static class Ttl
    {
        public static readonly TimeSpan VeryShort = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan Short      = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan Medium     = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan Long       = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan VeryLong   = TimeSpan.FromHours(24);
    }
}
```

```csharp
// Usage example
public class GetStorefrontQueryHandler(ICacheService cache, MarketNestDbContext db)
    : IQueryHandler<GetStorefrontQuery, StorefrontDto?>
{
    public Task<StorefrontDto?> Handle(GetStorefrontQuery q, CancellationToken ct)
        => cache.GetOrSetAsync(
            key: CacheKeys.Storefront(q.Slug),
            factory: () => db.Storefronts.AsNoTracking()
                              .Where(s => s.Slug == q.Slug)
                              .Select(s => new StorefrontDto(...))
                              .FirstOrDefaultAsync(ct)!,
            expiry: CacheKeys.Ttl.Short,
            ct: ct)!;
}
```

---

## 4. Event Bus Abstraction

### 4.1 IDomainEventPublisher (Phase 1: in-process; Phase 3: RabbitMQ)

```csharp
// Core/Common/Events/IDomainEventPublisher.cs
/// <summary>
/// Phase 1: wraps MediatR IPublisher (in-process)
/// Phase 3: wraps MassTransit (RabbitMQ outbox)
/// Callers never know which — swap implementation in DI config
/// </summary>
public interface IDomainEventPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent;
}

// Phase 1 implementation
public class MediatRDomainEventPublisher(IPublisher publisher) : IDomainEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct)
        where TEvent : IDomainEvent
        => publisher.Publish(domainEvent, ct);
}

// Phase 3 implementation (registered instead of MediatR version)
public class MassTransitDomainEventPublisher(IPublishEndpoint bus) : IDomainEventPublisher
{
    public Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct)
        where TEvent : IDomainEvent
        => bus.Publish(domainEvent, ct);
}
```

---

## 5. HTTP Context & Request Utilities

### 5.1 Extension Methods

```csharp
// Web/Infrastructure/Extensions/HttpRequestExtensions.cs
public static class HttpRequestExtensions
{
    /// <summary>True if request came from HTMX</summary>
    public static bool IsHtmx(this HttpRequest request)
        => request.Headers.ContainsKey("HX-Request");

    /// <summary>True if HTMX boosted link (not partial swap)</summary>
    public static bool IsHtmxBoosted(this HttpRequest request)
        => request.Headers["HX-Boosted"] == "true";

    /// <summary>True if HTMX partial request (not full page)</summary>
    public static bool IsHtmxPartial(this HttpRequest request)
        => request.IsHtmx() && !request.IsHtmxBoosted();

    /// <summary>Get client IP (respects X-Forwarded-For behind Nginx)</summary>
    public static string GetClientIp(this HttpRequest request)
    {
        var forwardedFor = request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
            return forwardedFor.Split(',')[0].Trim();
        return request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

// Web/Infrastructure/Extensions/ClaimsPrincipalExtensions.cs
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
        => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? throw new UnauthorizedException());

    public static string GetEmail(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;

    public static string GetRole(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public static bool IsAdmin(this ClaimsPrincipal user)
        => user.IsInRole("Admin");

    public static bool IsSeller(this ClaimsPrincipal user)
        => user.IsInRole("Seller");
}
```

### 5.2 Correlation ID Middleware

```csharp
// Web/Infrastructure/Middleware/CorrelationIdMiddleware.cs
public class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string CorrelationHeader = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationHeader].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N")[..8]; // short 8-char ID

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[CorrelationHeader] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (Activity.Current?.AddTag("correlationId", correlationId) is null ? default : default)
        {
            await next(context);
        }
    }
}
```

---

## 6. Error Handling Infrastructure

### 6.1 Global Exception → Problem Details

```csharp
// Web/Infrastructure/Middleware/ExceptionMiddleware.cs
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            context.Response.StatusCode = 422;
            await WriteProblemsAsync(context, new ValidationProblemDetails(
                ex.Errors.GroupBy(e => e.PropertyName)
                         .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            { Title = "Validation failed", Status = 422 });
        }
        catch (NotFoundException ex)
        {
            context.Response.StatusCode = 404;
            await WriteProblemsAsync(context, new ProblemDetails
                { Title = "Not found", Detail = ex.Message, Status = 404 });
        }
        catch (UnauthorizedException)
        {
            context.Response.StatusCode = 401;
            await WriteProblemsAsync(context, new ProblemDetails
                { Title = "Unauthorized", Status = 401 });
        }
        catch (ForbiddenException)
        {
            context.Response.StatusCode = 403;
            await WriteProblemsAsync(context, new ProblemDetails
                { Title = "Forbidden", Status = 403 });
        }
        catch (Exception ex)
        {
            var correlationId = context.Items["CorrelationId"]?.ToString();
            logger.LogError(ex, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);
            
            context.Response.StatusCode = 500;
            await WriteProblemsAsync(context, new ProblemDetails
            {
                Title  = "An unexpected error occurred",
                Status = 500,
                Extensions = { ["correlationId"] = correlationId }
            });
        }
    }

    private static Task WriteProblemsAsync(HttpContext ctx, ProblemDetails details)
    {
        ctx.Response.ContentType = "application/problem+json";
        return ctx.Response.WriteAsJsonAsync(details);
    }
}
```

---

## 7. Testing Utilities

### 7.1 Test Fixtures

```csharp
// Tests/Common/MarketNestWebAppFactory.cs
public class MarketNestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("marketnest_test")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redis.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Override connection strings with test containers
            services.RemoveAll<DbContextOptions<MarketNestDbContext>>();
            services.AddDbContext<MarketNestDbContext>(opt =>
                opt.UseNpgsql(_postgres.GetConnectionString()));

            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton<IConnectionMultiplexer>(
                ConnectionMultiplexer.Connect(_redis.GetConnectionString()));

            // Replace email service with no-op
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender, NoOpEmailSender>();

            // Deterministic time for tests
            services.RemoveAll<IDateTimeService>();
            services.AddSingleton<IDateTimeService>(
                new FakeDateTimeService(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc)));
        });
    }

    public async Task DisposeAsync()
    {
        await _postgres.StopAsync();
        await _redis.StopAsync();
    }
}
```

### 7.2 Test Data Builders (Fluent)

```csharp
// Tests/Common/Builders/OrderBuilder.cs
/// <summary>
/// Test data builder — creates valid domain objects for tests.
/// Never construct domain objects directly in tests — use builders.
/// </summary>
public class OrderBuilder
{
    private Guid   _buyerId   = Guid.NewGuid();
    private Guid   _storeId   = Guid.NewGuid();
    private Money  _unitPrice = Money.Of(10.00m);
    private int    _qty       = 1;
    private Address _address  = Addresses.Default;

    public OrderBuilder WithBuyer(Guid buyerId)       { _buyerId = buyerId; return this; }
    public OrderBuilder WithStore(Guid storeId)       { _storeId = storeId; return this; }
    public OrderBuilder WithUnitPrice(decimal amount) { _unitPrice = Money.Of(amount); return this; }
    public OrderBuilder WithQuantity(int qty)         { _qty = qty; return this; }

    public Order Build()
    {
        var cartSnapshot = new CartSnapshot(
            BuyerId: _buyerId,
            Items: [new CartItemSnapshot(Guid.NewGuid(), _storeId, "Test Product", _unitPrice, _qty)],
            ShippingAddress: _address);
        return Order.Create(cartSnapshot).Value;
    }

    // Convenience: build in specific state
    public Order BuildConfirmed()
    {
        var order = Build();
        order.Confirm();
        return order;
    }

    public Order BuildShipped()
    {
        var order = BuildConfirmed();
        order.MarkAsShipped("TRACK123");
        return order;
    }
}

// Shared test address
public static class Addresses
{
    public static readonly Address Default = new(
        "Test User", "123 Test St", null, "Singapore", "SG", "018956", "SG");
}
```

### 7.3 Integration Test Base

```csharp
// Tests/Common/IntegrationTestBase.cs
public abstract class IntegrationTestBase(MarketNestWebAppFactory factory)
    : IClassFixture<MarketNestWebAppFactory>
{
    protected readonly HttpClient Http = factory.CreateClient();

    // Helpers for auth
    protected async Task<HttpClient> AsAdminAsync()   => await AuthAs("admin@test.com", "Admin");
    protected async Task<HttpClient> AsSellerAsync()  => await AuthAs("seller@test.com", "Seller");
    protected async Task<HttpClient> AsBuyerAsync()   => await AuthAs("buyer@test.com", "Buyer");

    private async Task<HttpClient> AuthAs(string email, string role)
    {
        // Seed user, get JWT, set Authorization header
        var token = await factory.GetTestToken(email, role);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return client;
    }

    // DB access for setup/assertions
    protected async Task<T> DbQueryAsync<T>(Func<MarketNestDbContext, Task<T>> query)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketNestDbContext>();
        return await query(db);
    }

    protected async Task DbCommandAsync(Func<MarketNestDbContext, Task> command)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketNestDbContext>();
        await command(db);
        await db.SaveChangesAsync();
    }
}
```
