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
  - [6. Base Repository & Aggregate Repository](#6-base-repository--aggregate-repository)
    - [IBaseRepository\<T, TKey\>](#ibaserepositoryt-tkey)
    - [BaseRepository Implementation](#baserepository-implementation)
    - [Domain event dispatch](#domain-event-dispatch)
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
      - [1. Data Schema](#1-data-schema)
      - [2. Registered \& Planned Jobs](#2-registered--planned-jobs)
      - [3. Admin Job Operations Roadmap](#3-admin-job-operations-roadmap)
  - [17. Error Handling Strategy](#17-error-handling-strategy)
    - [Global Exception Handler (unexpected only)](#global-exception-handler-unexpected-only)
    - [Security Middleware Pipeline](#security-middleware-pipeline)
  - [18. Module Registration Convention](#18-module-registration-convention)
  - [19. Database Startup \& Seeding](#19-database-startup--seeding)
    - [DatabaseInitializer](#databaseinitializer)
    - [IDataSeeder Contract](#idataseeder-contract)
  - [20. Page Handler Contract](#20-page-handler-contract)
  - [21. Testing Requirements](#21-testing-requirements)
   - [22. Unit of Work & Transaction Management](#22-unit-of-work--transaction-management)
  - [23. Runtime Context (IRuntimeContext)](#23-runtime-context-iruntimecontext)
  - [Appendix: Module Contract Checklist](#appendix-module-contract-checklist)
  - [24. Sequence Service — Running Number Generation (ADR-040)](#24-sequence-service--running-number-generation-adr-040)

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
// Base.Common/Cqrs/ICommand.cs
public interface ICommand<TResult> : IRequest<Result<TResult, Error>> { }
public interface ICommand : ICommand<Unit> { }

// Base.Common/Cqrs/IQuery.cs
public interface IQuery<TResult> : IRequest<TResult> { }

// Base.Common/Cqrs/ICommandHandler.cs
public interface ICommandHandler<TCommand, TResult>
    : IRequestHandler<TCommand, Result<TResult, Error>>
    where TCommand : ICommand<TResult> { }

// Base.Common/Cqrs/IQueryHandler.cs
public interface IQueryHandler<TQuery, TResult>
    : IRequestHandler<TQuery, TResult>
    where TQuery : IQuery<TResult> { }

// Base.Domain/Events/IDomainEvent.cs
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

Modules NEVER reference each other's concrete classes. They use contracts in `MarketNest.Base.Common/Contracts/Contracts/`.

```csharp
// Base.Common/Contracts/IOrderCreationService.cs — Cart → Orders
// Base.Common/Contracts/IInventoryService.cs — Cart/Orders → Catalog
// Base.Common/Contracts/IPaymentService.cs — Orders → Payments
// Base.Common/Contracts/INotificationService.cs — All → Notifications (template-based dispatch, ADR-034)
// Base.Common/Contracts/IStorefrontReadService.cs — Payments → Catalog
// Base.Common/Contracts/IUserPreferencesReadService.cs — Any → Identity
// Base.Common/Contracts/INotificationPreferenceReadService.cs — Notifications → Identity (Phase 2)
// Base.Common/Contracts/IReferenceDataReadService.cs — Any → Admin (Tier 1 reference data)
// Base.Common/Contracts/Config/IOrderPolicyConfig.cs + IOrderPolicyConfigWriter.cs — Admin → Orders
// Base.Common/Contracts/Config/ICommissionConfig.cs + ICommissionConfigWriter.cs — Admin → Payments
// Base.Common/Contracts/Config/IStorefrontPolicyConfig.cs + IStorefrontPolicyConfigWriter.cs — Admin → Catalog
// Base.Common/Contracts/Config/IReviewPolicyConfig.cs + IReviewPolicyConfigWriter.cs — Admin → Reviews
```

### INotificationService — Template-Based Dispatch (ADR-034)

```csharp
// Base.Common/Contracts/INotificationService.cs
public interface INotificationService
{
    // Template-based dispatch — email and/or in-app per template Channel setting
    Task SendAsync(
        Guid recipientUserId,
        string templateKey,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken ct = default);

    // Send same notification to multiple recipients (e.g., order.placed → buyer + seller)
    Task SendToMultipleAsync(
        IEnumerable<Guid> recipientUserIds,
        string templateKey,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken ct = default);

    // Security emails — bypasses preference check entirely, always sent
    Task SendSecurityEmailAsync(
        string toEmail,
        string templateKey,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken ct = default);
}
```

Template keys are defined as constants in `NotificationTemplateKeys` in `Base.Common`:
```csharp
// Usage in domain event handler
var vars = new OrderPlacedVariables(
    OrderNumber: order.Number, BuyerName: buyer.Name, ... ).ToVariables();

await notifications.SendAsync(buyer.Id, NotificationTemplateKeys.OrderPlacedBuyer, vars, ct);
```

See `docs/notifications.md` for the full dispatch pipeline, template engine, and usage guide.

### Three-Tier Configuration Contracts (ADR-021)

```csharp
// Tier 1 — Reference Data (Admin owns DB, all modules read via contract)
public interface IReferenceDataReadService
{
    Task<IReadOnlyList<CountryDto>> GetCountriesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProductCategoryDto>> GetProductCategoriesAsync(CancellationToken ct = default);
    // ... GetGendersAsync, GetPhoneCountryCodesAsync, GetNationalitiesAsync
    // ... GetCountryAsync(code), GetCategoryAsync(id), GetCategoryBySlugAsync(slug)
}

// Tier 2 — Business Config (each module owns its DB table + Redis cache)
// Admin module injects both read and write contracts — never the concrete service.
public interface ICommissionConfig
{
    decimal DefaultRate { get; }
    Task<decimal> GetRateForSellerAsync(Guid sellerId, CancellationToken ct);
}
public interface ICommissionConfigWriter
{
    Task<Result<Unit, Error>> SetDefaultRateAsync(decimal rate, Guid adminId, CancellationToken ct);
    Task<Result<Unit, Error>> SetSellerOverrideAsync(Guid sellerId, decimal rate, DateTimeOffset from, Guid adminId, CancellationToken ct);
    Task<Result<Unit, Error>> RemoveSellerOverrideAsync(Guid sellerId, Guid adminId, CancellationToken ct);
}
// Same pattern for IOrderPolicyConfig/Writer, IStorefrontPolicyConfig/Writer, IReviewPolicyConfig/Writer

// Tier 3 — System Configuration (no DB, bound from appsettings.json, IOptions<T>)
// PlatformOptions, ValidationOptions, SecurityOptions
// Located in MarketNest.Web/Infrastructure/Options/
```

### ICacheService

```csharp
// Base.Common/Contracts/ICacheService.cs — implemented by Web/Infrastructure/RedisCacheService.cs
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null, CancellationToken ct = default) where T : class;
}
// All cache keys and TTL constants defined in Base.Common/CacheKeys.cs
// Read-through pattern: DB is always source of truth; Redis is performance layer only
```

**Communication pattern:** prefer domain events for async; use service interfaces for sync queries. Admin always uses contracts — never references module internals.

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

### 4.1 Common Shared DTOs (`Base.Common/Dtos/`)

Reusable DTOs shared across all modules and the UI layer. Namespace: `MarketNest.Base.Common`.

| Record | Purpose | Key Properties |
|--------|---------|----------------|
| `IdAndNameDto` | Minimal lookup (Guid key) | `Id`, `Name` |
| `IdAndNameIntDto` | Minimal lookup (int key) | `Id`, `Name` |
| `SelectOptionDto<TKey>` | Full dropdown/multi-select option | `Id`, `Name`, `Value?`, `Description?`, `Disabled` |
| `SelectOptionDto` | Alias: `SelectOptionDto<Guid>` | — |
| `SelectOptionIntDto` | Alias: `SelectOptionDto<int>` | — |
| `DocumentInfo` | File/document reference value object | `Id`, `FileName`, `FileType`, `FileSizeBytes`, `Url`, `Title?`, `UploadedAt?`, `FileSizeDisplay` (computed) |
| `TimestampDto` | Created/Updated audit display | `CreatedAt`, `UpdatedAt?` |
| `StatusDto` | Status badge for lists | `Code`, `Label`, `Color?` |

**When to use which:**
- **`IdAndNameDto`** — autocomplete suggestions, simple lookup lists, cross-module references where only id+name matter.
- **`SelectOptionDto`** — `<select>` dropdowns and multi-select components that need extra metadata (value/code, description, disabled state).
- **`DocumentInfo`** — any stored file reference (product images, dispute attachments, import receipts). Validated value object with constructor guards.
- **`StatusDto`** — rendering status badges with semantic color on list pages.
- **`TimestampDto`** — displaying "Created X ago / Updated Y ago" on list items.

```csharp
// Dropdown options for a select field
var options = await query.Select(x => new SelectOptionDto
{
    Id = x.Id, Name = x.Name, Value = x.Slug
}).ToListAsync(ct);

// Simple lookup list
var items = await query.Select(x => new IdAndNameDto { Id = x.Id, Name = x.Name }).ToListAsync(ct);

// Document reference
var doc = new DocumentInfo(fileId, "report.pdf", "application/pdf", 204800, "/files/report.pdf")
{
    Title = "Monthly Report",
    UploadedAt = DateTimeOffset.UtcNow
};
// doc.FileSizeDisplay → "200.0 KB"
```

---

## 5. Validation Contract

Every Command has a paired Validator. No exceptions.

```csharp
// Base.Common/Validation/ValidatorExtensions.cs
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

Canonical interface in `src/Base/MarketNest.Base.Infrastructure/Persistence/IBaseRepository.cs`
(namespace `MarketNest.Base.Infrastructure`).

```csharp
public interface IBaseRepository<TEntity, TKey> where TEntity : Entity<TKey>
{
    // ── Read (load-then-mutate) ───────────────────────────────────────────────
    Task<TEntity>  GetByKeyAsync(TKey id, CancellationToken ct = default);   // throws KeyNotFoundException
    Task<TEntity?> FindByKeyAsync(TKey id, CancellationToken ct = default);  // returns null
    Task<bool>     ExistsAsync(TKey id, CancellationToken ct = default);

    // ── Query helpers ─────────────────────────────────────────────────────────
    Task<long>             CountAsync(Expression<Func<TEntity, bool>>? where = null, CancellationToken ct = default);
    Task<bool>             AnyAsync(Expression<Func<TEntity, bool>>? where = null, CancellationToken ct = default);
    Task<TEntity?>         FirstOrDefaultAsync(Expression<Func<TEntity, bool>> where, CancellationToken ct = default);
    Task<List<TEntity>>    ListAsync(Expression<Func<TEntity, bool>>? where = null,
                               Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
                               CancellationToken ct = default);
    Task<PagedResult<TEntity>> GetPagedListAsync(int page, int pageSize,
                               Expression<Func<TEntity, bool>>? where = null,
                               Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
                               CancellationToken ct = default);
    IQueryable<TEntity>    GetQueryable(Expression<Func<TEntity, bool>>? where = null);

    // ── Write ─────────────────────────────────────────────────────────────────
    void   Add(TEntity entity);
    Task   AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);

    void   Update(TEntity entity);
    Task   UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);

    void   Remove(TEntity entity);
    Task   RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default);
}
```

> **Rule**: `Add`/`Update`/`Remove` are synchronous because EF Core only tracks the change in memory — actual persistence happens via `IUnitOfWork.CommitAsync()`.  
> `AddRangeAsync` / `UpdateRangeAsync` / `RemoveRangeAsync` accept `IEnumerable<TEntity>` for batch operations.  
> **Never call `SaveChangesAsync` directly** from handlers — the transaction filter calls `CommitAsync` automatically (except background jobs).

### BaseRepository Implementation

`BaseRepository<TEntity, TKey, TContext>` in `src/Base/MarketNest.Base.Infrastructure/Persistence/BaseRepository.cs`.
Each module provides a **2-line thin wrapper** pinning its own `DbContext`:

```csharp
// src/MarketNest.Catalog/Infrastructure/Persistence/BaseRepository.cs
public abstract class BaseRepository<TEntity, TKey>(CatalogDbContext db)
    : BaseRepository<TEntity, TKey, CatalogDbContext>(db);
```

### Domain event dispatch

Domain events are **no longer dispatched from the repository**. The `UnitOfWork` handles this automatically:
- **Pre-commit events** (`IPreCommitDomainEvent`) — dispatched inside the transaction before `SaveChangesAsync`
- **Post-commit events** (plain `IDomainEvent`) — dispatched after the transaction commits

See **§22 Unit of Work** for the full event lifecycle. Aggregates should raise events via `AddDomainEvent()` (on `AggregateRoot<TKey>`); the `UnitOfWork` scans the `ChangeTracker` to collect and dispatch them.

---

## 7. Base Query Handler — Paged Lists

```csharp
// Base.Common/Queries/PagedQuery.cs
public abstract record PagedQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public bool SortDesc { get; init; } = false;
    public string? Search { get; init; }
    public int Skip => (Page - 1) * PageSize;
}

// Base.Common/Queries/PagedResult.cs
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

> **Pre-commit vs post-commit split — see §22 for the full UoW pattern.**

Domain events are raised inside aggregate methods and dispatched by `UnitOfWork.CommitAsync()`:
- Events implementing **`IPreCommitDomainEvent`** run IN the open DB transaction before `SaveChanges`.
- All other `IDomainEvent` events run AFTER the transaction commits (post-commit).

### Phase 1 (In-process via MediatR via UnitOfWork)

```csharp
// Aggregate raises events
public class Order : AggregateRoot
{
    public void Place()
    {
        AddDomainEvent(new InventoryReservedEvent(Id));  // IPreCommitDomainEvent → atomic
        AddDomainEvent(new OrderPlacedEvent(Id, Total)); // IDomainEvent → post-commit (email etc.)
    }
}

// Command handler — DO NOT call uow.CommitAsync() or db.SaveChangesAsync() directly.
// The transaction filter handles commit automatically after the handler returns (ADR-027).
public async Task<Result<OrderDto, Error>> Handle(PlaceOrderCommand cmd, CancellationToken ct)
{
    var order = Order.Place(...);
    orders.Add(order);
    return Result.Ok(OrderDto.From(order));
    // Transaction filter will call CommitAsync/CommitTransactionAsync/DispatchPostCommitEventsAsync
}
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

### DDD Property Access Convention (ADR-023)

EF Core **natively supports `{ get; private set; }`** — no `{ get; set; }` is ever needed on domain entities.

**How it works** — EF Core uses the compiler-generated backing field (via reflection) to set property values during materialization. The `private set` accessor is never called by EF Core; it goes directly to the underlying field.

**The only special case** is **collection navigation properties** exposed as `IReadOnlyList<T>` with an explicit private backing field:

```csharp
// ✅ Correct: explicit backing field + read-only property
private readonly List<VoucherUsage> _usages = [];
public IReadOnlyList<VoucherUsage> Usages => _usages.AsReadOnly();

// ❌ Wrong: auto-property for collection navigations
public IReadOnlyList<VoucherUsage> Usages { get; private set; } = new List<VoucherUsage>();
```

These need `PropertyAccessMode.Field` so EF Core populates the backing field directly.

**Convention extension**: Call `ApplyDddPropertyAccessConventions()` in every `OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.HasDefaultSchema(TableConstants.Schema.MyModule);
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(MyModuleDbContext).Assembly);
    modelBuilder.ApplyDddPropertyAccessConventions(); // <-- always add this
    base.OnModelCreating(modelBuilder);
}
```

The extension (in `MarketNest.Base.Infrastructure`):
1. Sets model-level `PropertyAccessMode.PreferField` (explicit intent for DDD).
2. Auto-detects collection navigations with a matching `_camelCase` backing field and sets `PropertyAccessMode.Field`.

**Backing field naming convention**: `_camelCase` for `PascalCase` property (e.g., `_usages` for `Usages`).

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

### Trackable Interceptor (Audit Trail Stamping)

Automatic CreatedAt/ModifiedAt stamping for entities implementing `ITrackable` interface.

```csharp
// Sets CreatedAt/CreatedBy on Added, ModifiedAt/ModifiedBy on Modified
public sealed class TrackableInterceptor : SaveChangesInterceptor { ... }

public interface ITrackable
{
    DateTimeOffset CreatedAt { get; }
    Guid? CreatedBy { get; }         // null for system/seeded records
    DateTimeOffset? ModifiedAt { get; }
    Guid? ModifiedBy { get; }        // null until first modification
    
    void StampCreated(DateTimeOffset at, Guid? by);
    void StampModified(DateTimeOffset at, Guid? by);
}
```

**Registration** in module DbContext:
```csharp
protected override void OnConfiguring(DbContextOptionsBuilder options)
{
    options.AddInterceptors(new TrackableInterceptor());
}
```

**Usage**: Implement `ITrackable` on aggregate roots or entities needing automatic audit trails. Declare properties as `{ get; private set; }`, implement explicit interface methods that set the backing properties. The interceptor calls `StampCreated`/`StampModified` automatically on every save — application code never calls them.

### Audit Trail Interceptor (Detailed Change Logging)
```csharp
// Captures detailed change snapshots for entities marked [Auditable]
public partial class AuditableInterceptor(IAppLogger<AuditableInterceptor> logger) : SaveChangesInterceptor { ... }

// Mark entities: [Auditable] public class Product : AggregateRoot { }
// Interceptor writes change snapshots to AuditLog entity
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

**Recommended Table:** `admin.job_executions` (or `jobs.job_executions` if extracted as a dedicated module in Phase 3+).

#### 1. Data Schema

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

#### 2. Registered & Planned Jobs

| Job Name | Module | Schedule | Description |
| :--- | :--- | :--- | :--- |
| **ExpireSalesJob** | Catalog | Every 5 min | Clear `SalePrice/SaleStart/SaleEnd` on variants whose sale window ended. Raises `VariantSalePriceRemovedEvent`. JobKey: `catalog.variant.expire-sales` |
| **VoucherExpiryJob** | Promotions | Every hour | Set `Status = Expired/Depleted` on vouchers past `ExpiryDate` or fully consumed. |
| **CleanupExpiredReservations** | Cart | Every 5 min | Release DB reservations where Redis key expired |
| **AutoConfirmShippedOrders** | Orders | Daily 01:00 | SHIPPED > 30 days → DELIVERED |
| **AutoCompleteOrders** | Orders | Daily 01:05 | DELIVERED + 3 days no dispute → COMPLETED |
| **AutoCancelUnconfirmedOrders** | Orders | Every 30 min | CONFIRMED + 48h no seller action → CANCELLED |
| **ProcessPayoutBatch** | Payments | Daily 02:00 | Calculate payouts for COMPLETED orders |
| **SendNotificationDigests** | Notifications | Daily 08:00 | Review digest emails to Sellers |
| **ProcessHourlyNotificationDigest**| Notifications | Every hour | Batch notifications for OneHourDigest users |
| **ProcessDailyNotificationDigest** | Notifications | Daily 09:00 | Batch notifications (per user timezone) |
| **CleanupOrphanWishlistSnapshots**| Cart | Weekly | Remove wishlist items for deleted products |

---

#### 3. Admin Job Operations Roadmap

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

---

## 22. Unit of Work & Transaction Management

> ADR-027. Files: `Base.Domain/Events/IPreCommitDomainEvent.cs`, `Base.Domain/Events/IHasDomainEvents.cs`, `Base.Infrastructure/Persistence/IUnitOfWork.cs`, `Web/Infrastructure/Persistence/UnitOfWork.cs`, `Web/Infrastructure/Filters/`,  `Base.Common/Attributes/TransactionAttributes.cs`.

### Domain Event Lifecycle Split

Domain events raised by aggregates are partitioned into two categories at commit time:

| Category | Interface | When dispatched | Example |
|---|---|---|---|
| **Pre-commit** (executing) | `IPreCommitDomainEvent` | INSIDE the open DB transaction, before `SaveChanges` | `InventoryReservedEvent` |
| **Post-commit** (executed) | `IDomainEvent` (default) | AFTER the DB transaction commits successfully | `OrderPlacedEvent` (sends email) |

Post-commit failures are **logged but never roll back** the committed transaction. Phase 3 replaces post-commit dispatch with an Outbox pattern for guaranteed delivery.

### IUnitOfWork Contract

```csharp
// Base.Infrastructure  —  injected by transaction filters only
public interface IUnitOfWork : IAsyncDisposable
{
    // Transaction management (HTTP + background jobs)
    Task BeginTransactionAsync(IsolationLevel isolation = ReadCommitted, CancellationToken ct = default);
    Task CommitTransactionAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
    
    // Event & persistence management
    IReadOnlyList<IDomainEvent> CollectPreCommitEvents();
    Task<int> CommitAsync(CancellationToken ct = default);  // SaveChanges + pre-commit events
    Task DispatchPostCommitEventsAsync(CancellationToken ct = default);
}
```

**Rules for Command Handlers:**
- ✅ Mutate entities via repositories — no explicit transaction calls needed.
- ✅ The filter automatically calls `BeginTransactionAsync()`, `CommitAsync()`, `CommitTransactionAsync()`, `DispatchPostCommitEventsAsync()`, `DisposeAsync()`.
- ❌ Never call `dbContext.SaveChangesAsync()` directly.
- ❌ Never call `db.Database.BeginTransactionAsync()`.
- **Exception — background jobs**: Background jobs run outside the HTTP filter and must explicitly manage the full transaction lifecycle.

### Transaction Lifecycle (HTTP write request via filter)

```
Filter: BeginTransactionAsync on all module DbContexts
  ├─ Handler runs
  │  └─ aggregate.DoSomething()  →  RaiseDomainEvent(...)
  │     (no uow.CommitAsync call in handler)
  │
  ├─ Filter: CommitAsync()
  │  ├─ CollectPreCommitEvents()
  │  ├─ publisher.Publish(preCommitEvents)   ← INSIDE TX
  │  ├─ ClearDomainEvents()
  │  └─ SaveChangesAsync on all DbContexts   ← still INSIDE TX
  │
  ├─ Filter: CommitTransactionAsync()        ← COMMIT the DB transaction
  │
  ├─ Filter: DispatchPostCommitEventsAsync() ← AFTER commit (failures logged only)
  │
  └─ Filter: DisposeAsync()                  ← Cleanup transaction objects
```

### Background Job Transaction Lifecycle (explicit management)

```csharp
try {
    await uow.BeginTransactionAsync(ct: cancellationToken);
    // mutate entities via repositories
    await uow.CommitAsync(cancellationToken);              // SaveChanges + pre-commit events
    await uow.CommitTransactionAsync(cancellationToken);   // COMMIT
    await uow.DispatchPostCommitEventsAsync(cancellationToken);
} catch (Exception ex) {
    await uow.RollbackAsync(cancellationToken);  // rollback + clear events
    throw;
} finally {
    await uow.DisposeAsync();
}
```

### Filters

**`RazorPageTransactionFilter`** — registered globally via `Configure<MvcOptions>`. Wraps `OnPost*`, `OnPut*`, `OnDelete*`, `OnPatch*` automatically. `OnGet*` is never wrapped.

**`TransactionActionFilter`** — registered globally via `Configure<MvcOptions>`. Activates only when `[Transaction]` attribute is present on the controller class or action. `WriteApiV1ControllerBase` applies `[Transaction]` at class level.

### Attributes

```csharp
// Customize isolation level or timeout on a specific OnPost* or action
[Transaction(IsolationLevel.Serializable, timeoutSeconds: 60)]
public async Task<IActionResult> OnPostConfirmAsync(CancellationToken ct) { ... }

// Opt-out from auto-transaction
[NoTransaction]
public async Task<IActionResult> OnPostWebhookAsync(CancellationToken ct) { ... }
```

### Controller Base Classes

```csharp
// Read controllers — no transaction
public class MyReadController(IMediator mediator) : ReadApiV1ControllerBase(mediator) { }

// Write controllers — [Transaction] applied automatically
public class MyWriteController(IMediator mediator) : WriteApiV1ControllerBase(mediator) { }
```

### Command Handler Example

```csharp
public class PlaceOrderCommandHandler(IOrderRepository orders)
    : ICommandHandler<PlaceOrderCommand, Result<OrderDto, Error>>
{
    public async Task<Result<OrderDto, Error>> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var order = Order.Create(cmd);    // raises OrderPlacedEvent + InventoryReservedEvent
        orders.Add(order);
        return Result.Ok(OrderDto.From(order));
        // Filter will call CommitAsync/CommitTransactionAsync/DispatchPostCommitEventsAsync automatically
    }
}
```

**No explicit `uow.CommitAsync()` needed** — the transaction filter handles it after the handler returns.
```

---

## 23. Runtime Context (IRuntimeContext)

> **ADR-028** | Implemented 2026-04-29 | `Base.Common` (contracts) + `MarketNest.Web` (implementations)

`IRuntimeContext` is the **single injection point** that replaces scattered `ICurrentUserService` + manual `HttpContext.TraceIdentifier` calls across handlers, pages, and middlewares.

### Contracts (`MarketNest.Base.Common`)

| Type | Description |
|------|-------------|
| `ICurrentUser` | Authenticated user. Anonymous = `IsAuthenticated: false`, all nullable members `null`. |
| `IRuntimeContext` | Ambient request/job context: `CorrelationId`, `RequestId`, `CurrentUser`, `StartedAt`, HTTP metadata. |
| `RuntimeExecutionContext` | Enum: `HttpRequest`, `BackgroundJob`, `Test`. |
| `UnauthorizedException` | Thrown by `ICurrentUser.RequireId()` when user is anonymous. |

### Implementations (`MarketNest.Web.Infrastructure`)

| Class | Scope | Use case |
|-------|-------|----------|
| `HttpRuntimeContext` | Scoped | Mutable backing object populated by `RuntimeContextMiddleware`. Never inject directly — use `IRuntimeContext`. |
| `BackgroundJobRuntimeContext` | — | Immutable. Use `ForSystemJob(jobKey)` or `ForAdminJob(jobKey, adminId)` static factories in background jobs. |
| `CurrentUser` | Internal | ClaimsPrincipal-backed implementation. Has `IsAdmin`, `IsSeller`, `IsBuyer` computed properties. |

### Test helpers (`MarketNest.UnitTests`)

```csharp
TestRuntimeContext.AsAnonymous()
TestRuntimeContext.AsBuyer(buyerId)
TestRuntimeContext.AsSeller(sellerId)
TestRuntimeContext.AsAdmin(adminId)
```

### DI registration (Program.cs)

```csharp
builder.Services.AddScoped<HttpRuntimeContext>();
builder.Services.AddScoped<IRuntimeContext>(sp => sp.GetRequiredService<HttpRuntimeContext>());
builder.Services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<IRuntimeContext>().CurrentUser);
```

### Middleware pipeline

`RuntimeContextMiddleware` is registered **after** `UseAuthorization()` so JWT claims are already on `HttpContext.User`:

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RuntimeContextMiddleware>();  // populates IRuntimeContext + Serilog enrichment
```

The middleware:
1. Resolves or generates `X-Correlation-ID` and echoes it back on the response.
2. Builds `CurrentUser` from `ClaimsPrincipal`.
3. Sets Serilog `LogContext` properties (`CorrelationId`, `UserId`, `UserRole`) for the entire request.
4. Tags the OpenTelemetry `Activity` (`correlation.id`, `user.id`, `user.role`).
5. Logs request start/end (`LogEventId.RuntimeContextRequestStart/End`, 10700–10701).

### Usage patterns

**Command handler (authenticated write):**

```csharp
public class PlaceOrderCommandHandler(
    IOrderRepository orders,
    IRuntimeContext ctx) : ICommandHandler<PlaceOrderCommand, Result<OrderDto, Error>>
{
    public async Task<Result<OrderDto, Error>> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var buyerId = ctx.CurrentUser.RequireId();  // throws UnauthorizedException if anonymous
        // ... mutate via repository — no uow.CommitAsync() call needed (filter handles it)
    }
}
```

**Audit interceptor (must not throw):**

```csharp
public class AuditInterceptor(IRuntimeContext ctx) : SaveChangesInterceptor
{
    // Use IdOrNull (never RequireId) — background jobs have no user
    entry.Entity.CreatedBy = ctx.CurrentUser.IdOrNull;
}
```

**Background job:**

```csharp
public class ExpireSalesJob : IBackgroundJob
{
    public async Task ExecuteAsync(JobExecutionContext jobCtx, CancellationToken ct)
    {
        var runtimeCtx = BackgroundJobRuntimeContext.ForSystemJob(jobCtx.JobKey);
        // pass runtimeCtx to services that need it
    }
}
```

---

## 24. Sequence Service — Running Number Generation (ADR-040)

Deadlock-free, period-resettable running numbers via PostgreSQL `SEQUENCE`. Full documentation: `docs/sequence-service.md`.

### Contract (`Base.Common/Sequences/`)

- `SequenceResetPeriod` — enum: `Never`, `Monthly`, `Yearly`
- `SequenceDescriptor` — immutable record: schema, baseName, prefix, padWidth, resetPeriod
- `ISequenceService` — `NextFormattedAsync()`, `NextValueAsync()`, `ListSequenceNamesAsync()`, `DropSequenceAsync()`

### Usage

```csharp
// Define once per module (static descriptor)
public static class OrderSequences
{
    public static readonly SequenceDescriptor OrderNumber = new(
        schema: "orders", baseName: "ord", prefix: "ORD",
        padWidth: 5, resetPeriod: SequenceResetPeriod.Monthly);
}

// Use in handler
var number = await sequenceService.NextFormattedAsync(OrderSequences.OrderNumber, ct);
// → "ORD202604-00001"
```

### Registered Jobs

| Job Key | Schedule | Description |
|---|---|---|
| `common.cleanup-stale-sequences` | `0 2 1 * *` | Drops sequences older than retention window |

