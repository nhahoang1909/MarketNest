# MarketNest — Backend Patterns & Contracts

> Version: 0.1 | Status: Draft | Date: 2026-04
> Consolidated from: `backend-requirements.md` + `backend-infrastructure-foundations.md` + `contract-first-guide.md`
> **The golden rule: define the contract (interface) first. Implementation comes second.**

---

## Table of Contents

- [MarketNest — Backend Patterns \& Contracts](#marketnest--backend-patterns--contracts)
  - [Table of Contents](#table-of-contents)
  - [1. Technology Stack](#1-technology-stack)
  - [2. CQRS Contracts \& Result Pattern](#2-cqrs-contracts--result-pattern)
    - [Marker Interfaces](#marker-interfaces)
    - [Result\<T, Error\> — The Only Way to Return Errors](#resultt-error--the-only-way-to-return-errors)
    - [CQRS Usage Examples](#cqrs-usage-examples)
  - [3. Cross-Module Contracts](#3-cross-module-contracts)
  - [4. DTO Conventions](#4-dto-conventions)
  - [5. Validation Contract](#5-validation-contract)
  - [6. Base Repository \& Aggregate Repository](#6-base-repository--aggregate-repository)
    - [IBaseRepository\<T, TKey\>](#ibaserepositoryt-tkey)
    - [BaseRepository Implementation](#baserepository-implementation)
    - [AggregateRepository (domain event dispatch)](#aggregaterepository-domain-event-dispatch)
  - [7. Base Query Handler — Paged Lists](#7-base-query-handler--paged-lists)
    - [BasePagedQueryHandler](#basepagedqueryhandler)
  - [8. Base Command Handlers](#8-base-command-handlers)
    - [SimpleCommandHandler — For CRUD Screens](#simplecommandhandler--for-crud-screens)
    - [Full CommandHandler — For Domain Operations](#full-commandhandler--for-domain-operations)
  - [9. Common Service Interfaces](#9-common-service-interfaces)
  - [10. Domain Event Pattern](#10-domain-event-pattern)
    - [Phase 1 (In-process via MediatR)](#phase-1-in-process-via-mediatr)
    - [Phase 3+ (Outbox Pattern)](#phase-3-outbox-pattern)
  - [11. MediatR Pipeline Behaviors](#11-mediatr-pipeline-behaviors)
  - [12. Authentication \& Authorization](#12-authentication--authorization)
    - [JWT Configuration](#jwt-configuration)
    - [Role-Based Policies](#role-based-policies)
  - [13. Data Access Layer](#13-data-access-layer)
  - [14. EF Core Common Configurations](#14-ef-core-common-configurations)
    - [Soft Delete Interceptor](#soft-delete-interceptor)
    - [Audit Trail Interceptor](#audit-trail-interceptor)
    - [BaseEntityConfiguration](#baseentityconfiguration)
  - [15. Redis Integration](#15-redis-integration)
  - [16. Background Jobs](#16-background-jobs)
    - [Job Management Strategy](#job-management-strategy)
    - [Required Job Metadata](#required-job-metadata)
    - [Job Execution Data Model](#job-execution-data-model)
      - [1. Data Schema (Cấu trúc dữ liệu)](#1-data-schema-cấu-trúc-dữ-liệu)
      - [2. Planned Jobs (Danh sách Job dự kiến)](#2-planned-jobs-danh-sách-job-dự-kiến)
      - [3. Admin Job Operations Roadmap (Lộ trình phát triển)](#3-admin-job-operations-roadmap-lộ-trình-phát-triển)
  - [17. Error Handling Strategy](#17-error-handling-strategy)
    - [Global Exception Handler (unexpected only)](#global-exception-handler-unexpected-only)
    - [Security Middleware Pipeline](#security-middleware-pipeline)
  - [18. Module Registration Convention](#18-module-registration-convention)
  - [19. Database Startup \& Seeding](#19-database-startup--seeding)
    - [DatabaseInitializer](#databaseinitializer)
    - [IDataSeeder Contract](#idataseeder-contract)
  - [20. Page Handler Contract](#20-page-handler-contract)
  - [21. Testing Requirements](#21-testing-requirements)
  - [Appendix: Module Contract Checklist](#appendix-module-contract-checklist)

---

## 1. Technology Stack

| Technology | Version | Role |
|------------|---------|------|
| .NET | 10 LTS | Runtime |
| ASP.NET Core | 10 | Web framework (Razor Pages + minimal API) |
| Entity Framework Core | 10 | ORM for PostgreSQL |
| MediatR | 12.x | CQRS mediator + in-process events |
| FluentValidation | 11.x | Request/command validation |
| MassTransit | 8.x | Message bus (RabbitMQ, Phase 3+) |
| StackExchange.Redis | 2.x | Redis client |
| Serilog | 4.x | Structured logging |
| OpenTelemetry | 1.x | Distributed tracing + metrics |
| xUnit + FluentAssertions + Testcontainers | Latest | Testing |

---

## 2. CQRS Contracts & Result Pattern

### Marker Interfaces

```csharp
// Core/Common/Cqrs/ICommand.cs
public interface ICommand<TResult> : IRequest<Result<TResult, Error>> { }
public interface ICommand : ICommand<Unit> { }

// Core/Common/Cqrs/IQuery.cs
public interface IQuery<TResult> : IRequest<TResult> { }

// Core/Common/Cqrs/ICommandHandler.cs
public interface ICommandHandler<TCommand, TResult>
    : IRequestHandler<TCommand, Result<TResult, Error>>
    where TCommand : ICommand<TResult> { }

// Core/Common/Cqrs/IQueryHandler.cs
public interface IQueryHandler<TQuery, TResult>
    : IRequestHandler<TQuery, TResult>
    where TQuery : IQuery<TResult> { }

// Core/Common/Events/IDomainEvent.cs
public interface IDomainEvent : INotification
{
    Guid EventId => Guid.NewGuid();
    DateTime OccurredAt => DateTime.UtcNow;
}

public interface IDomainEventHandler<TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent { }
```

### Result<T, Error> — The Only Way to Return Errors

```csharp
public class Result<TValue, TError>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public TValue Value   => IsSuccess ? _value! : throw new InvalidOperationException("No value");
    public TError Error   => IsFailure ? _error! : throw new InvalidOperationException("No error");

    public static Result<TValue, TError> Success(TValue value) => new(value);
    public static Result<TValue, TError> Failure(TError error) => new(error);

    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<TError, TResult> onFailure)
        => IsSuccess ? onSuccess(_value!) : onFailure(_error!);

    public Result<TNew, TError> Map<TNew>(Func<TValue, TNew> mapper) => ...;
    public async Task<Result<TNew, TError>> MapAsync<TNew>(Func<TValue, Task<TNew>> mapper) => ...;
}

public record Error(string Code, string Message, ErrorType Type = ErrorType.Validation)
{
    public static Error NotFound(string entity, string id) => ...;
    public static Error Unauthorized(string? detail = null) => ...;
    public static Error Forbidden(string? detail = null) => ...;
    public static Error Conflict(string code, string message) => ...;
    public static Error Unexpected(string? detail = null) => ...;
}

public enum ErrorType { Validation, NotFound, Conflict, Unauthorized, Forbidden, Unexpected }
```

### CQRS Usage Examples

```csharp
// Command
public record PlaceOrderCommand(Guid BuyerId, Guid CartId, ShippingAddressDto ShippingAddress, string PaymentMethod)
    : ICommand<PlaceOrderResult>;

// Query (read-only, can use raw SQL / Dapper)
public record GetOrderDetailQuery(Guid OrderId, Guid RequestingUserId) : IQuery<OrderDetailDto>;
```

---

## 3. Cross-Module Contracts

Modules NEVER reference each other's concrete classes. They use contracts in `MarketNest.Core/Contracts/`.

```csharp
// Core/Contracts/IOrderCreationService.cs — Cart → Orders
public interface IOrderCreationService
{
    Task<Result<Guid, Error>> CreateFromCartAsync(Guid buyerId, CartSnapshot cart, Address shippingAddress, CancellationToken ct);
}

// Core/Contracts/IInventoryService.cs — Cart/Orders → Catalog
public interface IInventoryService
{
    Task<bool> HasStockAsync(Guid variantId, int quantity, CancellationToken ct);
    Task<Result<Unit, Error>> ReserveAsync(Guid variantId, int quantity, Guid cartId, CancellationToken ct);
    Task ReleaseAsync(Guid variantId, int quantity, CancellationToken ct);
    Task CommitAsync(Guid variantId, int quantity, CancellationToken ct);
}

// Core/Contracts/IPaymentService.cs — Orders → Payments
public interface IPaymentService
{
    Task<Result<Guid, Error>> CaptureAsync(Guid orderId, Money amount, string paymentMethod, CancellationToken ct);
    Task<Result<Unit, Error>> RefundAsync(Guid paymentId, Money amount, string reason, CancellationToken ct);
}

// Core/Contracts/INotificationService.cs — All → Notifications
public interface INotificationService
{
    Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct);
    Task SendTemplatedEmailAsync(string to, string templateName, object model, CancellationToken ct);
}

// Core/Contracts/IStorefrontReadService.cs — Payments → Catalog
public interface IStorefrontReadService
{
    Task<decimal> GetCommissionRateAsync(Guid storeId, CancellationToken ct);
    Task<StorefrontInfo?> GetBySlugAsync(string slug, CancellationToken ct);
}

// Core/Contracts/IUserPreferencesReadService.cs — Any module → Identity
public interface IUserPreferencesReadService
{
    Task<UserPreferencesSnapshot?> GetByUserIdAsync(Guid userId, CancellationToken ct);
}

// Core/Contracts/INotificationPreferenceReadService.cs — Notifications → Identity
public interface INotificationPreferenceReadService
{
    Task<NotificationPreferenceSnapshot?> GetByUserIdAsync(Guid userId, CancellationToken ct);
}
```

**Communication pattern:** prefer domain events for async; use service interfaces for sync queries.

---

## 4. DTO Conventions

```
✅ DTOs are records (immutable, { get; init; })
✅ DTOs live in the Application layer of the producing module
✅ Separate: Query DTOs (read) vs Command DTOs (write)
❌ Never expose domain entities outside the aggregate boundary
❌ Never reuse the same DTO for create and update

Naming: {Entity}Dto, {Entity}ListItemDto, {Entity}DetailDto
Commands: {Action}{Entity}Command : ICommand<TResult>
Snapshots: CartSnapshot, CartItemSnapshot (cross-module, serializable)
```

---

## 5. Validation Contract

Every Command has a paired Validator. No exceptions.

```csharp
// Core/Common/Validation/ValidatorExtensions.cs
public static class ValidatorExtensions
{
    public static IRuleBuilderOptions<T, string> MustBeSlug<T>(this IRuleBuilder<T, string> rule)
        => rule.NotEmpty().Matches(@"^[a-z0-9-]{3,50}$").WithMessage("3-50 lowercase/numbers/hyphens");

    public static IRuleBuilderOptions<T, decimal> MustBePositiveMoney<T>(this IRuleBuilder<T, decimal> rule)
        => rule.GreaterThan(0).LessThanOrEqualTo(999_999.99m);

    public static IRuleBuilderOptions<T, string> MustBeValidEmail<T>(this IRuleBuilder<T, string> rule)
        => rule.NotEmpty().EmailAddress().MaximumLength(254);

    public static IRuleBuilderOptions<T, Guid> MustBeValidId<T>(this IRuleBuilder<T, Guid> rule)
        => rule.NotEqual(Guid.Empty);

    public static IRuleBuilderOptions<T, int> MustBeValidQuantity<T>(this IRuleBuilder<T, int> rule)
        => rule.InclusiveBetween(1, 99);

    public static IRuleBuilderOptions<T, string> MustBeValidTimezone<T>(this IRuleBuilder<T, string> rule)
        => rule.NotEmpty().Must(tz => { try { TimeZoneInfo.FindSystemTimeZoneById(tz); return true; } catch { return false; } })
            .WithMessage("Must be a valid IANA timezone ID");

    public static IRuleBuilderOptions<T, string> MustBeValidCountryCode<T>(this IRuleBuilder<T, string> rule)
        => rule.NotEmpty().Length(2).Matches(@"^[A-Z]{2}$").WithMessage("Must be ISO 3166-1 alpha-2 (e.g., US, VN)");
}
```

---

## 6. Base Repository & Aggregate Repository

### IBaseRepository<T, TKey>

```csharp
public interface IBaseRepository<TEntity, TKey> where TEntity : Entity<TKey>
{
    Task<TEntity?> GetByKeyAsync(TKey id, CancellationToken ct = default);
    Task<TEntity>  GetByKeyOrThrowAsync(TKey id, CancellationToken ct = default);
    Task<bool>     ExistsAsync(TKey id, CancellationToken ct = default);
    void Add(TEntity entity);
    void Update(TEntity entity);
    void Remove(TEntity entity);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

### BaseRepository Implementation

```csharp
public abstract class BaseRepository<TEntity, TKey>(MarketNestDbContext db)
    : IBaseRepository<TEntity, TKey> where TEntity : Entity<TKey>
{
    protected readonly MarketNestDbContext Db = db;
    protected readonly DbSet<TEntity> Set = db.Set<TEntity>();

    public virtual async Task<TEntity?> GetByKeyAsync(TKey id, CancellationToken ct)
        => await Set.FirstOrDefaultAsync(e => e.Id!.Equals(id), ct);

    public virtual async Task<TEntity> GetByKeyOrThrowAsync(TKey id, CancellationToken ct)
        => await GetByKeyAsync(id, ct)
           ?? throw new NotFoundException(typeof(TEntity).Name, id!.ToString()!);

    // ... Add, Update, Remove, SaveChanges

    protected IQueryable<TEntity> Query()           => Set.AsNoTracking();
    protected IQueryable<TEntity> QueryTracked()    => Set;
    protected IQueryable<TEntity> QueryWithDeleted()=> Set.IgnoreQueryFilters();
}
```

### AggregateRepository (domain event dispatch)

```csharp
public abstract class AggregateRepository<TAggregate, TKey>(
    MarketNestDbContext db, IPublisher publisher)
    : BaseRepository<TAggregate, TKey>(db)
    where TAggregate : AggregateRoot<TKey>
{
    public new async Task<int> SaveChangesAsync(CancellationToken ct)
    {
        var aggregates = Db.ChangeTracker.Entries<AggregateRoot<TKey>>()
            .Where(e => e.Entity.DomainEvents.Any()).Select(e => e.Entity).ToList();
        var result = await Db.SaveChangesAsync(ct);
        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
                await publisher.Publish(domainEvent, ct);
            aggregate.ClearDomainEvents();
        }
        return result;
    }
}
```

---

## 7. Base Query Handler — Paged Lists

```csharp
// Core/Common/Queries/PagedQuery.cs
public abstract record PagedQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public bool SortDesc { get; init; } = false;
    public string? Search { get; init; }
    public int Skip => (Page - 1) * PageSize;
}

// Core/Common/Queries/PagedResult.cs
public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrev => Page > 1;
    public bool HasNext => Page < TotalPages;

    public static PagedResult<T> Empty(int page, int pageSize) => new() { ... };
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> mapper) => ...;
}
```

### BasePagedQueryHandler

```csharp
public abstract class BasePagedQueryHandler<TQuery, TDto>(MarketNestDbContext db)
    where TQuery : PagedQuery
{
    protected abstract IQueryable<TDto> BuildQuery(TQuery query);
    protected virtual IQueryable<TDto> ApplySort(IQueryable<TDto> query, TQuery request) => query;

    protected async Task<PagedResult<TDto>> GetPagedListAsync(TQuery request, CancellationToken ct)
    {
        var baseQuery = BuildQuery(request);
        var sorted    = ApplySort(baseQuery, request);
        var total = await sorted.CountAsync(ct);
        if (total == 0) return PagedResult<TDto>.Empty(request.Page, request.PageSize);
        var items = await sorted.Skip(request.Skip).Take(request.PageSize).ToListAsync(ct);
        return new PagedResult<TDto> { Items = items, Page = request.Page, PageSize = request.PageSize, TotalCount = total };
    }
}
```

---

## 8. Base Command Handlers

### SimpleCommandHandler — For CRUD Screens

```csharp
public abstract class SimpleCommandHandler<TCommand, TResult>
    : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public abstract Task<Result<TResult, Error>> Handle(TCommand command, CancellationToken ct);
    protected static Result<TResult, Error> Ok(TResult value) => Result.Success<TResult, Error>(value);
    protected static Result<TResult, Error> Fail(Error error) => Result.Failure<TResult, Error>(error);
}
```

### Full CommandHandler — For Domain Operations

```csharp
public abstract class CommandHandler<TCommand, TResult>
    : ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public abstract Task<Result<TResult, Error>> Handle(TCommand command, CancellationToken ct);
    protected static Result<TResult, Error> Ok(TResult value) => ...;
    protected static Result<TResult, Error> Fail(Error error) => ...;
}
```

---

## 9. Common Service Interfaces

```csharp
// ICurrentUserService — get authenticated user info
public interface ICurrentUserService
{
    Guid UserId { get; }
    string Email { get; }
    string Role  { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin  => Role == "Admin";
    bool IsSeller => Role == "Seller";
    Guid RequireUserId(); // throws if not authenticated
}

// IDateTimeService — testable time abstraction
public interface IDateTimeService
{
    DateTime UtcNow { get; }
    DateOnly TodayUtc => DateOnly.FromDateTime(UtcNow);
}

// ISlugService — URL-safe slug generation
public interface ISlugService
{
    string Generate(string input);
    Task<string> GenerateUniqueAsync(string input, Func<string, Task<bool>> existsCheck);
}

// IStorageService — file uploads (local Phase 1, S3/R2 Phase 2+)
public interface IStorageService
{
    Task<StorageResult> UploadAsync(Stream content, string fileName, string contentType, string folder, CancellationToken ct);
    Task DeleteAsync(string fileUrl, CancellationToken ct);
}
```

---

## 10. Domain Event Pattern

### Phase 1 (In-process via MediatR)

```csharp
// Aggregate raises event
public class Order : AggregateRoot
{
    public void Place() {
        AddDomainEvent(new OrderPlacedEvent(Id, BuyerId, SellerId, Total));
    }
}
// SaveChangesInterceptor or AggregateRepository dispatches after commit
```

### Phase 3+ (Outbox Pattern)

```csharp
// Save event to DB in same transaction → background job polls → publishes to RabbitMQ
// MassTransit Outbox integration with EF Core
```

---

## 11. MediatR Pipeline Behaviors

```csharp
// Order: Validation → Performance → Logging

// 1. ValidationBehavior — runs FluentValidation before any handler
// 2. PerformanceBehavior — warns on requests > 500ms
// 3. LoggingBehavior — structured debug logging

// Registration in Program.cs
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
});
```

---

## 12. Authentication & Authorization

### JWT Configuration
```
Access Token:   15 min expiry, RS256 (asymmetric)
Refresh Token:  7 days, stored in Redis, HttpOnly cookie
Refresh Flow:   POST /auth/refresh → validate Redis → issue new pair
Revocation:     DELETE Redis key on logout; blacklist on password change
```

### Role-Based Policies
```csharp
options.AddPolicy("SellerOnly", p => p.RequireRole("Seller"));
options.AddPolicy("AdminOnly",  p => p.RequireRole("Admin"));
options.AddPolicy("OwnStorefront", p => p.Requirements.Add(new OwnStorefrontRequirement()));
```

---

## 13. Data Access Layer

```csharp
// Shared DbContext with schema-per-module
public class MarketNestDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("public");
        builder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContextExtensions).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContextExtensions).Assembly);
    }
}

// Entity configuration example
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", "catalog");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
```

---

## 14. EF Core Common Configurations

### Soft Delete Interceptor
```csharp
// Intercepts EntityState.Deleted → sets IsDeleted=true, DeletedAt=UtcNow
public class SoftDeleteInterceptor : SaveChangesInterceptor { ... }

public interface ISoftDeletable
{
    bool IsDeleted { get; set; }       // { get; set; } per ADR-007 exception
    DateTime? DeletedAt { get; set; }
}
```

### Audit Trail Interceptor
```csharp
// Sets CreatedAt/CreatedBy on Added, UpdatedAt/UpdatedBy on Modified
public class AuditInterceptor(ICurrentUserService currentUser) : SaveChangesInterceptor { ... }

public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}
```

### BaseEntityConfiguration
```csharp
public abstract class BaseEntityConfiguration<TEntity, TKey> : IEntityTypeConfiguration<TEntity>
    where TEntity : Entity<TKey>
{
    // Auto-configures: Id key, audit columns, soft delete query filter
}
```

---

## 15. Redis Integration

```csharp
public class RedisCartReservationService : ICartReservationService
{
    private const int ReservationTtlSeconds = 900; // 15 min

    public async Task<bool> ReserveAsync(Guid userId, Guid variantId, int qty)
    {
        var key = $"marketnest:cart:{userId}:reservation:{variantId}";
        // Lua script for atomicity: check + set with TTL
    }

    public async Task RenewAsync(Guid userId, Guid variantId)
    {
        var key = $"marketnest:cart:{userId}:reservation:{variantId}";
        await _redis.KeyExpireAsync(key, TimeSpan.FromSeconds(ReservationTtlSeconds));
    }
}
```

---


## 16. Background Jobs

### Job Management Strategy

Background jobs must be observable and operable from the beginning, even if the full admin UI is deferred.

There are two job categories:

| Type | Description | Examples |
|------|-------------|----------|
| Timer Job | Predefined scheduled job owned by a module | Cleanup reservations, auto-complete orders, notification digest |
| Batch Job | Explicit one-off or queued operation, usually admin/system triggered | Recalculate payouts, reindex products, bulk notification send |

Phase strategy:

| Phase | Scope |
|-------|-------|
| Phase 1 | Define contracts, metadata, execution log model, and module registration convention |
| Phase 2 | Admin dashboard: list jobs, view executions, inspect failures, retry failed executions, manual trigger safe jobs |
| Phase 3+ | Dynamic batch registration, RabbitMQ/MassTransit worker execution, distributed locking, service split support |

### Required Job Metadata

Every background job must expose stable metadata:

```csharp
public sealed record JobDescriptor(
    string JobKey,
    string DisplayName,
    string OwningModule,
    JobType Type,
    string? Schedule,
    bool IsEnabled,
    bool IsRetryable,
    int MaxRetryCount,
    string Description);
```

``` csharp
public enum JobType
{
    Timer = 1,
    Batch = 2
}
```

Rules:
JobKey must be stable and globally unique, for example: orders.auto-complete-orders.
OwningModule must match the module boundary, for example: Orders, Cart, Notifications.
Timer jobs are registered by code, not created dynamically by admin users in Phase 1.
Batch jobs may support dynamic registration only in Phase 3+.
Jobs exposed to retry or manual trigger must be idempotent.
Job Execution Status

``` csharp
public enum JobExecutionStatus
{
Pending = 1,
Running = 2,
Succeeded = 3,
Failed = 4,
Cancelled = 5,
Skipped = 6
}
```

Core Contracts

``` csharp
public interface IBackgroundJob
{
    JobDescriptor Descriptor { get; }

    Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken);
}

public sealed record JobExecutionContext(
    Guid ExecutionId,
    string JobKey,
    Guid? TriggeredByUserId,
    JobTriggerSource TriggerSource,
    Guid? RetryOfExecutionId,
    IReadOnlyDictionary<string, string> Parameters);
```

``` csharp
public enum JobTriggerSource
{
    System = 1,
    Admin = 2,
    Retry = 3
}
```

``` csharp
public interface IJobRegistry
{
    IReadOnlyCollection<JobDescriptor> GetJobs();

    JobDescriptor? FindByKey(string jobKey);
}
```

``` csharp
public interface IJobExecutionStore
{
    Task<Guid> CreateExecutionAsync(
        JobDescriptor descriptor,
        JobExecutionContext context,
        CancellationToken cancellationToken);

    Task MarkRunningAsync(
        Guid executionId,
        DateTime startedAtUtc,
        CancellationToken cancellationToken);

    Task MarkSucceededAsync(
        Guid executionId,
        DateTime finishedAtUtc,
        CancellationToken cancellationToken);

    Task MarkFailedAsync(
        Guid executionId,
        DateTime finishedAtUtc,
        string errorMessage,
        string? errorDetails,
        CancellationToken cancellationToken);
}
```
### Job Execution Data Model

**Recommended Table:** `admin.job_executions` (hoặc `jobs.job_executions` nếu mở rộng quy mô trong tương lai).

#### 1. Data Schema (Cấu trúc dữ liệu)

| Column | Description |
| :--- | :--- |
| **Id** | Execution ID (Primary Key) |
| **JobKey** | Stable identifier for the job |
| **JobType** | `Timer` or `Batch` |
| **OwningModule** | Module that owns the job |
| **Status** | `Pending`, `Running`, `Succeeded`, `Failed`, `Cancelled`, `Skipped` |
| **TriggeredByUserId** | Admin user ID (if manually triggered) |
| **TriggerSource** | `System`, `Admin`, `Retry` |
| **RetryOfExecutionId** | ID of the original failed execution (if applicable) |
| **StartedAtUtc** | Execution start time |
| **FinishedAtUtc** | Execution end time |
| **DurationMs** | Execution duration in milliseconds |
| **ErrorMessage** | Short failure summary |
| **ErrorDetails** | Full error details or stack trace |
| **ParametersJson** | Batch input parameters |
| **CreatedAtUtc** | Record creation timestamp |

---

#### 2. Planned Jobs (Danh sách Job dự kiến)

| Job Name | Schedule | Description |
| :--- | :--- | :--- |
| **CleanupExpiredReservations** | Every 5 min | Release DB reservations where Redis key expired |
| **AutoConfirmShippedOrders** | Daily 01:00 | SHIPPED > 30 days → DELIVERED |
| **AutoCompleteOrders** | Daily 01:05 | DELIVERED + 3 days no dispute → COMPLETED |
| **AutoCancelUnconfirmedOrders** | Every 30 min | CONFIRMED + 48h no seller action → CANCELLED |
| **ProcessPayoutBatch** | Daily 02:00 | Calculate payouts for COMPLETED orders |
| **SendNotificationDigests** | Daily 08:00 | Review digest emails to Sellers |
| **ProcessHourlyNotificationDigest**| Every hour | Batch notifications for OneHourDigest users |
| **ProcessDailyNotificationDigest** | Daily 09:00 | Batch notifications (per user Timezone) |
| **CleanupOrphanWishlistSnapshots**| Weekly | Remove wishlist items for deleted products |

---

#### 3. Admin Job Operations Roadmap (Lộ trình phát triển)

| Capability | Phase | Notes |
| :--- | :--- | :--- |
| **View registered jobs** | Phase 2 | Read from IJobRegistry |
| **View execution history** | Phase 2 | Read from IJobExecutionStore |
| **See failed job details** | Phase 2 | Error message + stack trace |
| **Retry failed execution** | Phase 2 | Only if IsRetryable = true |
| **Manual trigger safe timer job**| Phase 2 | Only idempotent jobs |
| **Enable / disable timer job** | Phase 2+ | Persisted job settings |
| **Register new batch job** | Phase 3+ | Requires queue-backed worker and validation |
| **Distributed execution lock** | Phase 3+ | Needed after multi-node deployment |
| **Dedicated worker service** | Phase 3+ | Useful when jobs become heavy |

---

---


## 17. Error Handling Strategy

### Global Exception Handler (unexpected only)
```csharp
// Maps exceptions to Problem Details (RFC 7807):
// ValidationException → 422, NotFoundException → 404,
// UnauthorizedException → 401, ForbiddenException → 403, else → 500
```

### Security Middleware Pipeline
```csharp
app.UseHttpsRedirection();
app.UseHsts();
app.UseStaticFiles();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapRazorPages();
app.MapHealthChecks("/health");
```

---

## 18. Module Registration Convention

```csharp
// Each module: {Module}/Infrastructure/DependencyInjection.cs
public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IDataSeeder, CategorySeeder>();
        return services;
    }
}

// Program.cs — clean composition root
builder.Services
    .AddInfrastructure(builder.Configuration)
    .AddIdentityModule()
    .AddCatalogModule()
    .AddCartModule()
    .AddOrdersModule()
    .AddPaymentsModule()
    .AddReviewsModule()
    .AddDisputesModule()
    .AddNotificationsModule();
```

---

## 19. Database Startup & Seeding

### DatabaseInitializer

```csharp
public class DatabaseInitializer(MarketNestDbContext db, IEnumerable<IDataSeeder> seeders, ...)
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        // 1. Apply pending EF Core migrations
        // 2. Run seeders in priority order (skip non-production seeders in prod)
    }
}
```

### IDataSeeder Contract

```csharp
public interface IDataSeeder
{
    int Order { get; }             // 100=Roles, 200=Admin, 300=Categories, 400=Demo
    bool RunInProduction { get; }  // true = safe (idempotent, reference data)
    Task SeedAsync(CancellationToken ct);
}
```

Seeders: `RoleSeeder(100)` → `AdminUserSeeder(200)` → `CategorySeeder(300)` → `DemoDataSeeder(400, dev only)`

---

## 20. Page Handler Contract

```csharp
// Extension methods for Razor Page handlers
public static class PageHandlerExtensions
{
    // Render partial if HTMX, full page otherwise
    public static IActionResult Page(this PageModel page, bool isHtmx, string partialName, object? model = null);

    // Handle Result<T, Error> → success action or ModelState error
    public static IActionResult HandleResult<T>(this PageModel page, Result<T, Error> result, ...);

    // Redirect on success, re-render with error on failure
    public static IActionResult RedirectOnSuccess<T>(this PageModel page, Result<T, Error> result, ...);
}
```

---

## 21. Testing Requirements

```csharp
// Unit: domain logic testable without infrastructure
[Fact]
public void Order_CannotTransitionToShipped_WhenNotConfirmed()
{
    var order = Order.Create(buyerId, cartSnapshot);
    var result = order.MarkAsShipped("TRACK123");
    result.IsFailure.Should().BeTrue();
}

// Integration: Testcontainers + WebApplicationFactory
public class OrderApiTests : IClassFixture<MarketNestWebAppFactory> { }

// Architecture: NetArchTest
[Fact]
public void DomainLayer_ShouldNotDependOn_Infrastructure() { }
```

---

## Appendix: Module Contract Checklist

When creating a new module, define ALL contracts before implementation:

```
DOMAIN: Entities, Value Objects, Domain Events, Invariants
APPLICATION: Commands, Queries, Validators, DTOs, cross-module deps
INFRASTRUCTURE: Repository interface, EF config, Redis keys, background jobs
WEB: Pages/routes, HTMX partials, form models
```

