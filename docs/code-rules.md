# MarketNest — Code Rules & Conventions

> Version: 0.1 | Status: Draft | Date: 2026-04  
> These rules are enforced by architecture tests, linters, and PR checklist.

---

## 1. General Principles

1. **Boring is good**: prefer simple, explicit, readable code over clever code
2. **Fail fast**: validate at system boundaries (API layer), not deep in business logic
3. **No magic**: avoid reflection-heavy frameworks, dynamic proxies, implicit behaviors you can't grep for
4. **One way to do things**: pick a pattern and use it consistently (e.g., always Result<T>, never throw for business failures)
5. **Make wrong states unrepresentable**: use value objects, sealed discriminated unions, private setters

---

## 2. .NET / C# Conventions

### 2.1 English Only

All source code **must** be written in English. This applies to:

- Class, method, property, variable, and parameter **names**
- Code **comments** and XML doc comments
- Commit messages and PR descriptions
- Error messages and log messages in code
- String constants and enum member names
- Test method names and test display names

The only exceptions are **user-facing localization resource files** (`Resources/*.resx`) which contain translated strings for supported locales (English `en`, Vietnamese `vi`).

```csharp
// ✅ DO — English everywhere
public class OrderService { }
// Calculates the total commission for the seller
public decimal CalculateCommission(decimal total) { }

// ❌ DON'T — Vietnamese or any non-English in code
public class DichVuDonHang { }
// Tính tổng hoa hồng cho người bán
public decimal TinhHoaHong(decimal tongTien) { }
```

### 2.2 Naming
```csharp
// ✅ DO
public class PlaceOrderCommand { }           // Commands: verb + noun + Command
public class PlaceOrderCommandHandler { }    // Handlers: same + Handler
public class PlaceOrderCommandValidator { }  // Validators: same + Validator
public record OrderPlacedEvent { }           // Events: past tense + Event
public interface IOrderRepository { }        // Interfaces: I-prefix
public class OrderRepository : IOrderRepository { } // Implementations: no suffix
public record OrderDetailDto { }             // DTOs: Entity + "Dto" / "ListItemDto" / "DetailDto"
public record GetOrderDetailQuery { }        // Queries: "Get" + What + "Query"

// ❌ DON'T
public class OrderManager { }                // Vague — banned suffixes: Manager, Helper, Utils
public class OrderService { }                // "Service" suffix: too generic
public class HandleOrder { }                 // Inconsistent naming
public class OrderRepositoryImpl { }         // "Impl" suffix unnecessary
public class OrderDto { }                    // Too generic — use ListItemDto or DetailDto
```

**Private fields** use `_camelCase`:
```csharp
// ✅ DO
private readonly IOrderRepository _orders;
private readonly ILogger<PlaceOrderCommandHandler> _logger;

// ❌ DON'T
private IOrderRepository orders;      // missing underscore prefix
private IOrderRepository Orders;      // PascalCase for private field
private IOrderRepository m_orders;    // m_ prefix not used in this project
```

### 2.3 C# Features to Use
```csharp
// ✅ Records for immutable data (commands, queries, DTOs, value objects)
public record Money(decimal Amount, string Currency)
{
    public static Money Of(decimal amount, string currency)
    {
        if (amount < 0) throw new DomainException("Money amount cannot be negative");
        return new Money(amount, currency);
    }
}

// ✅ Result<T> instead of exceptions for business failures
public Result<Order, Error> PlaceOrder(PlaceOrderCommand command)
{
    if (!inventory.HasStock(command.VariantId, command.Quantity))
        return Result.Failure<Order, Error>(Errors.Cart.InsufficientStock);
    // ...
}

// ✅ Pattern matching for state machines
public Result<Unit, Error> Transition(OrderStatus newStatus) =>
    (Status, newStatus) switch
    {
        (OrderStatus.Pending,    OrderStatus.Confirmed)   => Confirm(),
        (OrderStatus.Confirmed,  OrderStatus.Processing)  => Process(),
        (OrderStatus.Processing, OrderStatus.Shipped)     => Ship(),
        _                                                  => 
            Result.Failure<Unit, Error>(Errors.Order.InvalidTransition(Status, newStatus))
    };

// ✅ Primary constructors (.NET 10)
public class OrderRepository(MarketNestDbContext db) : IOrderRepository
{
    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
        => await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
}

// ❌ Avoid nullable reference confusion — always initialize collections
public class Order
{
    private readonly List<OrderLine> _lines = [];  // ✅ not null
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
}
```

### 2.4 Async Rules
```csharp
// ✅ Always propagate CancellationToken through entire call chain
public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default) { }

// ✅ ConfigureAwait(false) in library/infrastructure code (not in PageModels)
var result = await db.SaveChangesAsync(ct).ConfigureAwait(false);

// ❌ Never block async code
var result = GetOrderAsync().Result;   // DEADLOCK risk
var result = GetOrderAsync().GetAwaiter().GetResult(); // same
Task.Run(() => GetOrderAsync()).Wait(); // worse

// ❌ Never use async void (exceptions are swallowed, cannot be awaited)
public async void HandleOrder() { }   // WRONG — use async Task

// ❌ Don't wrap synchronous values in Task.FromResult with async/await
public async Task<int> GetCountAsync() => await Task.FromResult(42); // unnecessary
public int GetCount() => 42; // ✅ if there is no I/O, don't make it async

// ❌ Never fire-and-forget — missing await silently ignores exceptions
SendEmailAsync(email); // WRONG — missing await
await SendEmailAsync(email); // ✅
```

// ✅ Prefer async/await everywhere
// Always prefer using `async`/`await` and propagate `CancellationToken` instead of blocking on tasks
// with `.Result` or `GetAwaiter().GetResult()`. Blocking a task can cause thread-pool exhaustion
// or deadlocks (especially in sync-over-async scenarios). If you must call async code from
// synchronous entry points (very rare), wrap carefully and prefer rewriting the caller to be async.
// Example (preferred):
// var order = await GetOrderAsync(id, ct);
// Example (avoid):
// var order = GetOrderAsync(id).GetAwaiter().GetResult(); // BAD — can deadlock

// ⚠️ Exception: startup/bootstrapping code
// In very rare cases (application bootstrap or tooling that runs synchronously at process start)
// a sync-over-async call may be used. These occurrences must be documented with a clear comment
// explaining why the synchronous call is necessary and approved during code review. Prefer
// redesigning the startup to be async if feasible.


### 2.9 CancellationToken — mandatory policy

Cooperative cancellation is required across the codebase. The following rules are mandatory and enforced by code review and agent checks:

- Public API methods that perform I/O or long-running work MUST accept a `CancellationToken` parameter (preferably named `ct` or `cancellationToken`). Private helper methods that are not part of an API surface may omit the parameter only when cancellation is not meaningful.
- All methods declared on interfaces or abstract classes that represent asynchronous or cancellable work MUST include a `CancellationToken` parameter in their signature so callers and implementors can observe cancellation.
- Base classes (protected or public virtual/abstract methods) that are part of an overridable API MUST include a `CancellationToken` parameter so overrides can honor cancellation.
- MediatR handlers, controller/PageModel handlers, hosted services, background workers and pipeline behaviors MUST accept a `CancellationToken` parameter and forward it to all downstream async calls (repositories, queries, HttpClient, DbContext, IUnitOfWork, etc.). Handlers must pass the received token into repository and unit-of-work calls (for example `await repository.GetByIdAsync(id, ct)` and `await _unitOfWork.SaveChangesAsync(ct)`).
- When providing overloads, prefer the overload that accepts a `CancellationToken` and chain to it (avoid duplicating logic without token propagation).
- Avoid defaulting `CancellationToken` to `default` on public APIs if callers are likely to want cancellation. Place the `CancellationToken` as the last parameter.

Examples:
```csharp
// ✅ Interface with CancellationToken
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct);
}

// ✅ Handler forwards token to repository and unit-of-work
public class PlaceOrderCommandHandler : ICommandHandler<PlaceOrderCommand, PlaceOrderResult>
{
    public async Task<Result<PlaceOrderResult, Error>> Handle(PlaceOrderCommand command, CancellationToken ct)
    {
        var order = await _orders.GetByIdAsync(command.OrderId, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Result.Success(new PlaceOrderResult(order.Id));
    }
}
```

Enforcement notes:

- Add a check in the PR checklist and agent rules ensuring interface/abstract/base method signatures include `CancellationToken` where appropriate.
- For generated code or scaffolding, ensure templates include `CancellationToken` in public async method signatures.
- Consider adding a Roslyn analyzer (recommended) to automatically detect public async methods or interface/abstract method signatures missing a `CancellationToken`, and to detect failure to forward tokens inside handlers.


### 2.5 Dependency Injection
```csharp
// ✅ Register by interface, inject by interface
services.AddScoped<IOrderRepository, OrderRepository>();

// ✅ Use primary constructor injection (.NET 10) — not traditional constructor + field assignment
public class PlaceOrderCommandHandler(
    IOrderRepository orders,
    ICartReservationService reservations,
    IPublisher publisher) : ICommandHandler<PlaceOrderCommand, PlaceOrderResult>
{ }

// ❌ Never use service locator
var repo = serviceProvider.GetService<IOrderRepository>(); // anti-pattern

// ❌ Never inject implementation instead of interface
public class Handler(OrderRepository repo) { } // WRONG — use IOrderRepository
```

**Lifetime conventions:**

| Lifetime | When to use |
|----------|-------------|
| Scoped | DbContext, Repositories, Application Services, MediatR handlers, UnitOfWork |
| Transient | Validators, lightweight stateless services |
| Singleton | Cache wrappers, configuration readers, `IHttpClientFactory` |

> ⚠️ **Captive dependency**: A Singleton must never inject a Scoped dependency. This causes the Scoped service to live as long as the Singleton, leading to stale data and concurrency bugs.

**Module DI registration pattern** — each module exposes an extension method, called from `Program.cs`:
```csharp
// src/MarketNest.Orders/DependencyInjection.cs
public static class DependencyInjection
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IDataSeeder, OrderStatusSeeder>();
        return services;
    }
}
```

### 2.6 No Magic Strings / Magic Numbers
```csharp
// ❌ Magic strings — hard to refactor, easy to mistype
if (order.Status == "Shipped") { }
var key = $"marketnest:cart:{userId}";
Response.Headers["HX-Redirect"] = "/orders/detail";

// ✅ Use enums for finite sets of values
public enum OrderStatus { Pending, Confirmed, Processing, Shipped, Delivered, Completed, Cancelled }
if (order.Status == OrderStatus.Shipped) { }

// ✅ Use constants for string/numeric literals
public static class RedisKeys
{
    private const string Prefix = "marketnest";
    public static string Cart(Guid userId) => $"{Prefix}:cart:{userId}";
    public static string RefreshToken(string tokenId) => $"{Prefix}:refresh:{tokenId}";
}

// ✅ Use constants for route paths and configuration keys
public static class Routes
{
    public const string OrderDetail = "/orders/detail";
    public const string AdminDashboard = "/admin/dashboard";
}

// ❌ Magic numbers — unexplained numeric literals
if (retryCount > 3) { }
var commission = total * 0.05m;
Thread.Sleep(5000);

// ✅ Named constants with clear intent
public static class PolicyConstants
{
    public const int MaxRetryAttempts = 3;
    public const decimal DefaultCommissionRate = 0.05m;
    public const int RetryDelayMilliseconds = 5000;
}

// ✅ Also acceptable: well-named configuration options bound from appsettings
public record CommissionOptions
{
    public const string SectionName = "Commission";
    public decimal DefaultRate { get; init; } = 0.05m;
}
```

**Rule**: Every string literal used more than once and every "unexplained" number **must** be extracted to a `const`, `static readonly`, enum, or configuration option. Exceptions: `0`, `1`, `-1`, `string.Empty`, and obvious boolean comparisons.

### 2.7 Flat Layer-Level Namespaces

Namespaces within a module stop at the **layer level** (Application, Domain, Infrastructure). Sub-folders beneath a layer do **not** add to the namespace.

```csharp
// Module: MarketNest.Admin
// File: MarketNest.Admin/Application/Commands/CreateProductCommandHandler.cs

// ✅ Namespace matches the layer — not the folder hierarchy
namespace MarketNest.Admin.Application;

// ❌ Do NOT include sub-folder names in the namespace
namespace MarketNest.Admin.Application.Commands;    // WRONG
namespace MarketNest.Admin.Application.Queries;     // WRONG
namespace MarketNest.Admin.Domain.Entities;         // WRONG
namespace MarketNest.Admin.Infrastructure.Persistence; // WRONG
```

**Allowed namespace levels per module:**
| Namespace | What lives here |
|-----------|----------------|
| `MarketNest.<Module>` | Module root (AssemblyReference, DI registration) |
| `MarketNest.<Module>.Domain` | Entities, aggregates, value objects, domain events, repository interfaces |
| `MarketNest.<Module>.Application` | Commands, queries, handlers, validators, DTOs |
| `MarketNest.<Module>.Infrastructure` | EF configurations, repository implementations, external service adapters |

Sub-folders (e.g., `Commands/`, `Queries/`, `Entities/`, `Persistence/`) are for **file organization only** — they must not appear in the namespace.

**Rule**: When creating a new file, always use the layer-level namespace (`MarketNest.<Module>.Application`, `MarketNest.<Module>.Domain`, or `MarketNest.<Module>.Infrastructure`). Never append folder names beyond the layer.

### Module sub-folder layout and mapping

Modules often contain feature-level sub-folders to organize code (for example `Modules/Account`, `Modules/Product`). This repository uses a folder-per-feature layout for developer ergonomics but retains the flat layer-level namespace convention.

Example folder structure (allowed):

```
src/MarketNest.Admin/
  Common/
  Modules/
    Account/
      Commands/
      CommandHandlers/
      QueryHandlers/
      DomainEventHandlers/
      IntegrationEventHandlers/
  Infrastructure/
    Persistence/
    Messaging/
  Application/
  Domain/
```

Mapping rule (important):
  - Files under `src/MarketNest.Admin/Modules/Account/Commands/` or `.../CommandHandlers/` should use the layer-level namespace `MarketNest.Admin.Application` — do NOT include `Account` or `Commands` in the namespace.
  - Files under `src/MarketNest.Admin/Domain/Entities/` should use `MarketNest.Admin.Domain`.
  - Files under `src/MarketNest.Admin/Infrastructure/Persistence/` should use `MarketNest.Admin.Infrastructure`.

Examples:
 - File: `src/MarketNest.Admin/Modules/Account/Commands/CreateAccountCommand.cs`
   - Namespace: `namespace MarketNest.Admin.Application;`
  - File: `src/MarketNest.Admin/Modules/Account/CommandHandlers/CreateAccountCommandHandler.cs`
   - Namespace: `namespace MarketNest.Admin.Application;`
  - File: `src/MarketNest.Admin/Domain/Entities/Account.cs`
   - Namespace: `namespace MarketNest.Admin.Domain;`
  - File: `src/MarketNest.Admin/Infrastructure/Persistence/AdminDbContext.cs`
   - Namespace: `namespace MarketNest.Admin.Infrastructure;`

Note on tooling: some analyzers or IDE rules require file-path-to-namespace correspondence and may emit warnings when namespaces do not match folder hierarchy. These warnings are benign for this project because we intentionally decouple folder layout from namespaces for module-level clarity. If your IDE flags these as warnings, either adjust your local analyzer settings or ignore them — do not change namespaces to include folder names. If CI contains rules that enforce folder-based namespaces, coordinate with the team to update the rule to accept layer-level namespaces.

### 2.8 Date & Time — Always Use `DateTimeOffset`

All date/time fields in entities, value objects, events, DTOs, and configuration **must** use `DateTimeOffset`, never `DateTime`. This ensures timezone information is always preserved and supports user-configurable time zone and format preferences.

```csharp
// ✅ DO — DateTimeOffset everywhere
public class Order : AggregateRoot
{
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ShippedAt { get; private set; }
}

public record OrderPlacedEvent(Guid OrderId, DateTimeOffset OccurredAt) : IDomainEvent;

public record OrderDetailDto
{
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}

// ❌ DON'T — DateTime loses timezone context
public class Order
{
    public DateTime CreatedAt { get; private set; }  // BAD — no timezone info
}
```

**Formatting for display**: Always convert to user's local time before formatting. Use the extension methods in `DateTimeOffsetExtensions`:

```csharp
// ✅ Use extension methods with user's TimeZoneInfo
var userTz = userTimeZoneProvider.TimeZone;
var dateStr  = order.CreatedAt.FormatAsDateOnly(userTz);    // "2026-04-25"
var timeStr  = order.CreatedAt.FormatAsDateTime(userTz);    // "2026-04-25 14:30"
var relative = order.CreatedAt.FormatAsRelative();           // "5m ago"

// ❌ DON'T — raw ToString without timezone conversion
var bad = order.CreatedAt.ToString("yyyy-MM-dd");  // BAD — uses server time
```

**Rules:**
- All new date/time fields use `DateTimeOffset` — no exceptions
- Store timestamps in UTC (`DateTimeOffset.UtcNow`) — convert to local only at display time
- Use `IUserTimeZoneProvider` (from DI) to get the user's preferred time zone and format
- Use `DateTimeOffsetExtensions` methods for consistent formatting across the app
- Format constants live in `DomainConstants.DateTimeFormats`

---

## 3. Domain Layer Rules

### 3.1 Aggregate & Entity Integrity — Property Accessor Convention (ADR-007)

All domain types follow strict property accessor rules:

```csharp
// ═══════════════════════════════════════════════════════════════════
// ENTITIES & AGGREGATE ROOTS → { get; private set; }
// State changes ONLY through explicit domain methods.
// ═══════════════════════════════════════════════════════════════════

// ✅ Aggregates own their state — no public setters
public class Order : AggregateRoot
{
    public OrderStatus Status { get; private set; }
    public Money Total { get; private set; }
    public DateTimeOffset? ShippedAt { get; private set; }

    // All mutations go through explicit methods
    public Result<Unit, Error> MarkAsShipped(string trackingNumber) { ... }
}

// ✅ Child entities also use { get; private set; }
public class OrderLine : Entity<Guid>
{
    public Guid OrderId { get; private set; }
    public Money UnitPrice { get; private set; }
    public int Quantity { get; private set; }
}

// ✅ Base entity Id uses { get; protected set; } — exception for inheritance
public abstract class Entity<TKey>
{
    public TKey Id { get; protected set; } = default!;
}

// ❌ No anemic domain model
public class Order
{
    public OrderStatus Status { get; set; }  // Anyone can change state — BAD
}

// ❌ No { get; init; } on entities — bypasses domain method guards
public class Order
{
    public OrderStatus Status { get; init; }  // Settable in object initializer — BAD
}

// ═══════════════════════════════════════════════════════════════════
// VALUE OBJECTS (class-based, extending ValueObject) → { get; }
// Set only via constructor. Immutable after creation.
// ═══════════════════════════════════════════════════════════════════

// ✅ Class-based value object — readonly properties
public class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }
}

// ═══════════════════════════════════════════════════════════════════
// VALUE OBJECTS (record-based) → positional or { get; init; }
// Records are immutable by convention. Positional records give { get; init; }.
// ═══════════════════════════════════════════════════════════════════

// ✅ Positional record value object (yields { get; init; })
public record Address(string Street, string City, string State, string PostalCode, string Country);

// ✅ Record with explicit { get; } for validated VOs
public record Rating
{
    public int Value { get; }
    public Rating(int value) { /* validation */ Value = value; }
}

// ═══════════════════════════════════════════════════════════════════
// DTOs / Commands / Queries → record with { get; init; }
// Immutable after creation, settable during initialization.
// ═══════════════════════════════════════════════════════════════════

// ✅ DTOs and commands use record with init properties
public record OrderDetailDto(Guid Id, string Status, decimal Total);
public record PlaceOrderCommand(Guid BuyerId, Guid CartId) : ICommand<PlaceOrderResult>;

// ═══════════════════════════════════════════════════════════════════
// INFRASTRUCTURE INTERFACES → { get; set; } allowed
// EF Core interceptors need write access (ISoftDeletable, IAuditable).
// ═══════════════════════════════════════════════════════════════════

// ✅ Infrastructure interface — { get; set; } is acceptable
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
}
```

**Summary Table:**

| Type | Accessor | Reason |
|------|----------|--------|
| Entity / Aggregate Root property | `{ get; private set; }` | Protect invariants; mutate only via domain methods |
| Entity base `Id` | `{ get; protected set; }` | Allow derived classes to initialize |
| Value Object (class) property | `{ get; }` | Fully immutable; set via constructor only |
| Value Object (record) property | `{ get; init; }` or `{ get; }` | Immutable; positional records default to `init` |
| DTO / Command / Query | `{ get; init; }` (record) | Immutable after creation |
| Infrastructure interface | `{ get; set; }` | EF interceptors need write access |

> **EF Core compatibility (ADR-023):** `{ get; private set; }` is fully supported by EF Core — it uses the compiler-generated backing field to set values during materialization. **No `{ get; set; }` is needed on entities.**

**Collection navigation pattern**: always use an explicit backing field, never an auto-property:
```csharp
// ✅ Correct: explicit backing field + read-only property
private readonly List<OrderLine> _lines = [];
public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

// ❌ Wrong: auto-property for collection navigations
public IReadOnlyList<OrderLine> Lines { get; private set; } = new List<OrderLine>();
```

The `ApplyDddPropertyAccessConventions()` extension auto-detects backing fields (`_camelCase` for `PascalCase` property) and configures `PropertyAccessMode.Field` on those navigations. See `docs/backend-patterns.md` §14.

### 3.2 Domain Events
```csharp
// ✅ Raise events inside aggregate methods (not in handlers)
public Result<Unit, Error> Complete()
{
    if (Status != OrderStatus.Delivered)
        return Error.InvalidTransition;
    
    Status = OrderStatus.Completed;
    CompletedAt = DateTimeOffset.UtcNow;
    
    AddDomainEvent(new OrderCompletedEvent(Id, BuyerId, SellerId, Total));
    return Result.Success();
}
```

### 3.3 No Infrastructure References in Domain
```csharp
// ❌ NEVER in Domain project
using Microsoft.EntityFrameworkCore; // Domain knows nothing about EF
using StackExchange.Redis;           // or Redis
using System.Net.Http;               // or HTTP

// ✅ Domain only depends on:
using System;
using System.Collections.Generic;
using MarketNest.Core; // shared kernel only
```

---

## 4. Application Layer Rules

### 4.1 Command / Query Separation
```csharp
// Commands: change state, return Result (never void)
public interface ICommandHandler<TCommand, TResult>
{
    Task<Result<TResult, Error>> Handle(TCommand command, CancellationToken ct);
}

// Queries: never change state, return data
public interface IQueryHandler<TQuery, TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken ct);
}

// ❌ Don't mix: a query that creates a record, or a command that returns a list
```

**Command handler rules:**
- Delegate business logic to domain methods — the handler orchestrates, it does not decide
- Call `IUnitOfWork.SaveChangesAsync(ct)`, not `dbContext.SaveChangesAsync(ct)` directly
- Never validate inputs — that is the FluentValidation pipeline's responsibility
- Never raise domain events — the aggregate raises events inside its own methods
- Always propagate `CancellationToken ct` to every async call

**Query handler rules:**
- Always use `AsNoTracking()` on read queries
- Use `Select()` projection — do not `.Include()` + manually map
- Return DTOs, never domain entities
- Must never call `SaveChanges` — queries are strictly read-only
- Check ownership in `Where` clauses to prevent IDOR vulnerabilities

### 4.2 Validation
```csharp
// ✅ FluentValidation for all commands (registered as pipeline behavior)
public class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.BuyerId).NotEmpty().WithMessage("BuyerId is required");
        RuleFor(x => x.CartId).NotEmpty().WithMessage("CartId is required");
        RuleFor(x => x.ShippingAddress.Street)
            .NotEmpty().MaximumLength(200)
            .WithMessage("Street address must not exceed 200 characters");
    }
}
// Validation happens in MediatR pipeline BEFORE handler — handler never validates
```

**Rules:**
- Every Command **must** have a paired `{CommandName}Validator` — even simple commands
- Every validation rule **must** have an explicit `.WithMessage()` — auto-generated messages are unclear
- Validators contain only input validation (format, length, required) — complex business rules belong in the domain
- Validators are registered via assembly scanning (auto-discovery), not manually one-by-one
- **Handlers must never validate** — if you see `if (string.IsNullOrEmpty(...))` in a handler, move it to the Validator

---

## 5. Infrastructure Layer Rules

### 5.1 Repository Rules
```csharp
// ✅ Repository only for aggregates (not for every entity)
// ✅ Queries bypass repository — use DbContext directly or Dapper
// ❌ Don't put business logic in repositories
// ❌ Don't return IQueryable from repositories (leaks EF into domain)

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(Order order);
    // No: Task<IEnumerable<Order>> GetAllAsync(); — use a Query handler
}
```

### 5.2 EF Core Rules
```csharp
// ✅ AsNoTracking() for all queries (read-only operations)
var dto = await db.Orders.AsNoTracking()
                         .Where(o => o.BuyerId == buyerId)
                         .Select(o => new OrderSummaryDto(...))
                         .ToListAsync(ct);

// ✅ Never use raw SQL — always EF parameterized queries
// Exception: complex reporting queries with Dapper (still parameterized)

// ✅ Soft delete via query filter (not physical delete)
builder.HasQueryFilter(o => !o.IsDeleted);

// ✅ Schema-per-module
modelBuilder.HasDefaultSchema("orders"); // in OrderDbContext

// ❌ Never .Include() 3+ levels deep — use dedicated queries or projections

// ❌ Never call SaveChanges multiple times in one request — batch into one UoW call
await db.SaveChangesAsync(); // 1st call
await db.SaveChangesAsync(); // 2nd call — WRONG, consolidate into single uow.SaveChangesAsync()

// ❌ Module must not access another module's schema directly
// e.g., Orders module must NOT use db.Products — use cross-module interface or domain event
```

**Migration naming convention**: `YYYYMMDD_HHmm_Description` (e.g., `20260410_1430_AddOrderDispute`)

---

## 6. Web / Razor Pages Rules

### 6.1 Page Handler Conventions
```csharp
// ✅ Thin page model — delegate to MediatR immediately
// ✅ Always accept CancellationToken ct in handler methods
public class OrderDetailModel(IMediator mediator) : PageModel
{
    public OrderDetailDto Order { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(Guid orderId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetOrderDetailQuery(orderId, User.GetUserId()), ct);
        if (result is null) return NotFound();
        Order = result;
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid orderId, CancellationToken ct)
    {
        var result = await mediator.Send(new CancelOrderCommand(orderId, User.GetUserId()), ct);
        return result.Match(
            _ => RedirectToPage("./Index"),
            error => BadRequest(error.Message)  // or re-render with error
        );
    }
}

// ❌ Don't put business logic in page models
// ❌ Don't call repositories directly from page models
```

### 6.2 HTMX Response Conventions
```csharp
// ✅ For HTMX partial requests, return partial view
if (Request.IsHtmx())
    return Partial("_OrderList", model);
return Page();

// ✅ For redirects after POST (HTMX-compatible)
Response.Headers["HX-Redirect"] = Url.Page("/Orders/Detail", new { orderId });
return Ok();

// ✅ For error handling with HTMX
Response.Headers["HX-Retarget"] = "#error-container";
Response.Headers["HX-Reswap"] = "innerHTML";
return Partial("_Error", new ErrorViewModel(error.Message));
```

---

## 7. CSS / Frontend Style Rules

### 7.1 Prefer CSS Custom Properties (Variables) Over Hard-Coded Values

All visual values — colors, spacing, radii, shadows, transitions, font sizes — **must** reference CSS custom properties (`var(--*)`) defined in `input.css` or `components.css`. Hard-coded hex/rgb/hsl values, pixel literals, and repeated magic values in component markup or `.cshtml` inline styles are banned.

**Why**: A single change to a CSS variable propagates everywhere. Hard-coded values create invisible coupling and make redesigns painful.

```css
/* ✅ DO — reference design tokens via CSS variables */
.card-promo {
    background: var(--color-surface-muted);
    border: 1px solid var(--color-ink-100);
    border-radius: var(--radius-card);
    padding: var(--spacing-card);
    box-shadow: var(--shadow-sm);
    transition: border-color var(--transition-fast);
}

/* ❌ DON'T — hard-coded values scattered across components */
.card-promo {
    background: #faf8f4;
    border: 1px solid rgba(26, 31, 27, 0.12);
    border-radius: 20px;
    padding: 24px;
    box-shadow: 0 2px 8px rgba(0,0,0,0.06);
    transition: border-color 0.2s ease;
}
```

### 7.2 Define Reusable Token Layers

Organize CSS variables into **semantic layers** so components never reference raw palette values directly:

```css
/* Layer 1: Primitive palette — already defined in @theme (input.css) */
--color-ink-700: #1a1f1b;
--color-accent-400: #c8f25c;

/* Layer 2: Semantic tokens — map primitives to purpose */
--color-text-primary: var(--color-ink-700);
--color-text-secondary: var(--color-ink-400);
--color-text-muted: var(--color-ink-300);
--color-border: var(--color-ink-100);
--color-border-hover: var(--color-ink-300);
--color-bg-page: var(--color-cream-100);
--color-bg-card: var(--color-surface);
--color-bg-inset: var(--color-surface-inset);
--color-interactive: var(--color-accent-400);

/* Layer 3: Component tokens — scoped to specific components */
--btn-bg: var(--color-ink-700);
--btn-text: var(--color-cream-100);
--btn-bg-hover: var(--color-interactive);
--card-radius: var(--radius-card);
--card-padding: var(--spacing-card);
```

Components reference **Layer 2 or 3** tokens — never Layer 1 primitives. This enables dark mode, theming, and redesigns by swapping only the semantic layer.

### 7.3 Extract Repeated Magic Values into Variables

Any visual value that appears in **two or more places** must be extracted to a CSS variable:

```css
/* ✅ DO — shared spacing, radius, shadow, and transition tokens in @theme or :root */
@theme {
    --radius-sm: 10px;
    --radius-md: 14px;
    --radius-lg: 20px;
    --radius-full: 9999px;
    --radius-card: 20px;
    --radius-card-admin: 14px;

    --spacing-card: 24px;
    --spacing-card-admin: 20px;

    --shadow-sm: 0 2px 8px rgba(0, 0, 0, 0.06);
    --shadow-md: 0 8px 24px rgba(26, 31, 27, 0.08), 0 2px 6px rgba(26, 31, 27, 0.04);
    --shadow-lg: 0 12px 32px rgba(26, 31, 27, 0.12);

    --transition-fast: 0.15s ease;
    --transition-base: 0.2s ease;
    --transition-smooth: 0.3s cubic-bezier(0.22, 0.61, 0.36, 1);
}

/* ❌ DON'T — same border-radius and shadow copy-pasted across 10 components */
.card       { border-radius: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
.product-card { border-radius: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
.modal      { border-radius: 20px; box-shadow: 0 2px 8px rgba(0,0,0,0.06); }
```

### 7.4 Inline Styles in `.cshtml` Must Use Variables

When Razor Pages or server-rendered markup needs inline styles (e.g., dynamic backgrounds), always reference CSS variables:

```html
<!-- ✅ DO — inline style references a CSS variable -->
<div style="background: var(--color-surface-muted); border-radius: var(--radius-card);">
    ...
</div>

<!-- ❌ DON'T — hard-coded hex in Razor markup -->
<div style="background: #faf8f4; border-radius: 20px;">
    ...
</div>
```

**Exception**: Truly dynamic values computed by the server (e.g., progress bar width from a percentage) may use inline pixel/percent values, but colors and static dimensions must still be variables.

### 7.5 Keep `AppConstants.Colors` in Sync with CSS Tokens

Server-side inline color constants in `AppConstants.Colors` must mirror the CSS custom properties in `input.css`. When updating one, update the other. Add a comment referencing the corresponding CSS variable name:

```csharp
public static class Colors
{
    public const string Accent400 = "#c8f25c"; // --color-accent-400
    public const string Ink700 = "#1a1f1b";     // --color-ink-700
}
```

**Summary checklist:**
- No raw hex/rgb/hsl values in component CSS — use `var(--*)`
- No repeated `border-radius`, `box-shadow`, or `transition` literals — extract to token variables
- Inline styles in `.cshtml` reference CSS variables, not hard-coded values
- `AppConstants.Colors` stays in sync with `input.css` `@theme` tokens
- New component? Define component-level `--component-*` variables that reference semantic tokens

---

## 8. Error Codes Convention

All errors use `DOMAIN.ENTITY_ERROR` format:

```csharp
public static class Errors
{
    public static class Order
    {
        public static Error NotFound(Guid id) => 
            new("ORDER.NOT_FOUND", $"Order {id} not found", ErrorType.NotFound);
        
        public static Error InvalidTransition(OrderStatus from, OrderStatus to) =>
            new("ORDER.INVALID_TRANSITION", $"Cannot transition from {from} to {to}", ErrorType.Conflict);
        
        public static Error DisputeWindowClosed =>
            new("ORDER.DISPUTE_WINDOW_CLOSED", "Dispute window has expired", ErrorType.Conflict);
    }
    
    public static class Cart
    {
        public static Error InsufficientStock =>
            new("CART.INSUFFICIENT_STOCK", "Not enough stock available", ErrorType.Conflict);
        
        public static Error ReservationExpired =>
            new("CART.RESERVATION_EXPIRED", "Cart reservation has expired", ErrorType.Conflict);
    }
}
```

---

## 9. Logging Standards

> **Mandatory pattern**: all production logging must use `[LoggerMessage]` source-generated delegates (CA1848).
> Direct calls to `_logger.Info(...)` / `_logger.LogInformation(...)` are banned in new code. See ADR-014.

### 9.1 Log levels

| Level | When to use |
|-------|-------------|
| `Debug` | Developer diagnostics — disabled in prod |
| `Information` | Business events (order placed, payment captured) |
| `Warning` | Handled errors, business rejections, slow queries |
| `Error` | Unexpected failures, unhandled exceptions |
| `Critical` | System down, data corruption risk |

### 9.2 Mandatory [LoggerMessage] pattern

Every class that emits logs must:

1. Add `partial` to the class declaration
2. Inject `IAppLogger<T>` (not `ILogger<T>`) via primary constructor
3. Create `private static partial class Log` at the bottom of the file
4. Call only `Log.Xxx(_logger, ...)` — never call `_logger.LogXxx(...)` extension methods directly

```csharp
public partial class OrderDetailModel(IAppLogger<OrderDetailModel> _logger, IMediator mediator) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid orderId, CancellationToken ct)
    {
        var correlationId = HttpContext.TraceIdentifier;
        Log.InfoStart(_logger, orderId, correlationId);

        var sw = Stopwatch.StartNew();
        var result = await mediator.Send(new GetOrderDetailQuery(orderId), ct);
        sw.Stop();

        if (result is null)
        {
            Log.WarnNotFound(_logger, orderId, correlationId);
            return NotFound();
        }

        Log.InfoSuccess(_logger, orderId, sw.ElapsedMilliseconds, correlationId);
        return Page();
    }

    private static partial class Log
    {
        [LoggerMessage((int)LogEventId.AccountOrdersDetailStart, LogLevel.Information,
            "OrderDetail Start - OrderId={OrderId} CorrelationId={CorrelationId}")]
        public static partial void InfoStart(ILogger logger, Guid orderId, string correlationId);

        [LoggerMessage((int)LogEventId.AccountOrdersDetailSuccess, LogLevel.Information,
            "OrderDetail Success - OrderId={OrderId} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId}")]
        public static partial void InfoSuccess(ILogger logger, Guid orderId, long elapsedMs, string correlationId);

        [LoggerMessage((int)LogEventId.AccountOrdersDetailNotFound, LogLevel.Warning,
            "OrderDetail NotFound - OrderId={OrderId} CorrelationId={CorrelationId}")]
        public static partial void WarnNotFound(ILogger logger, Guid orderId, string correlationId);
    }
}
```

### 9.3 Delegate naming convention

Method names follow `{LogLevel}{Subject}{Event}`:

| Example | When |
|---------|------|
| `InfoStart` | Entry point of a handler |
| `InfoOrderPlaced` | Happy path success |
| `WarnCartItemOutOfStock` | Business rejection |
| `ErrorPaymentGatewayTimeout` | Unexpected failure |

### 9.4 Rules

- **Exception always last** — never include `{Exception}` in the template; the last `Exception` param is automatically attached to the log event
- **No PII** — log IDs only; never log email, full name, address, card numbers
- **No anonymous objects** — `new { OrderId, BuyerId }` → separate typed params
- **Template must be a const string** — no interpolation (`$"..."`) inside `[LoggerMessage]` attribute
- **No raw EventId integers** — always reference `LogEventId` enum: `[LoggerMessage((int)LogEventId.OrderDetailStart, ...)]`. Enum defined at `MarketNest.Base.Infrastructure/Logging/LogEventId.cs`
- **EventId increments sequentially within each file** — Start=X, Success=X+1, Warn=X+2, Error=X+3
- **No `IsEnabled` guard needed** — `[LoggerMessage]` skips automatically when level is disabled

### 9.5 EventId allocation per module

Each module owns a block of 1000 EventIds. Sub-allocation within each block:

| Offset | Layer |
|--------|-------|
| X000–X199 | Infrastructure / Persistence |
| X200–X599 | Application layer (Command/Query handlers) |
| X600–X799 | Web Pages (PageModel handlers) |
| X800–X999 | Reserved |

| Module | EventId Range |
|--------|--------------|
| Infrastructure / Middleware | 1000–1999 |
| Identity | 2000–2999 |
| Catalog | 3000–3999 |
| Cart | 4000–4999 |
| Orders | 5000–5999 |
| Payments | 6000–6999 |
| Reviews | 7000–7999 |
| Disputes | 8000–8999 |
| Notifications | 9000–9999 |
| Admin | 10000–10999 |
| Auditing | 11000–11999 |
| Background Jobs | 12000–12999 |
| Web / Global Pages | 13000–13999 |

---

## 10. Git Commit Conventions

Follow **Conventional Commits**:
```
feat(orders): add dispute window expiry check
fix(cart): release reservation on TTL expiry
refactor(payments): extract commission calculation to value object
test(orders): add integration test for auto-complete job
docs(api): update order state machine diagram
chore(deps): upgrade EF Core to 10.1
perf(catalog): add index on products.status for listing query
```

---

## 11. Code Review Checklist (PR Gate)

Before merging:
- [ ] All naming, comments, error messages, and log messages are in English (see §2.1)
- [ ] Private fields use `_camelCase` naming (see §2.2)
- [ ] No banned class suffixes: Manager, Helper, Utils (see §2.2)
- [ ] Domain layer has zero infrastructure dependencies (architecture test passes)
- [ ] All commands/queries validated by FluentValidation with explicit `.WithMessage()` (see §4.2)
- [ ] Every Command has a paired Validator (see §4.2)
- [ ] Handlers contain no validation or business logic — delegates to domain (see §4.1)
- [ ] SaveChanges called via `IUnitOfWork`, not `dbContext` directly (see §4.1)
- [ ] No raw SQL strings (Dapper queries use parameters)
- [ ] No public setters on aggregates
- [ ] Entity properties use `{ get; private set; }` — no `{ get; set; }` or `{ get; init; }` (ADR-007)
- [ ] Value object properties use `{ get; }` (class-based) or `{ get; init; }` (record-based) (ADR-007)
- [ ] No async void (except event handlers)
- [ ] No blocking of async operations via `.Result` or `GetAwaiter().GetResult()` — prefer `async`/`await` (see §2.4)
- [ ] No `await Task.FromResult(...)` — remove async if no I/O (see §2.4)
- [ ] Public async/cancellable APIs include `CancellationToken` (interfaces/abstract/base methods included)
- [ ] Handlers and PageModel/Controller methods forward `CancellationToken` to downstream calls (repositories, IUnitOfWork, DbContext)
- [ ] `AsNoTracking()` on all read queries (see §5.2)
- [ ] Query handlers return DTOs, not domain entities (see §4.1)
- [ ] DI uses primary constructor injection and interface-based registration (see §2.5)
- [ ] Each module has `DependencyInjection.cs` with `Add{Module}Module()` extension (see §2.5)
- [ ] No service locator pattern (`GetService<T>` in handlers) (see §2.5)
- [ ] New business rules have unit tests
- [ ] New API endpoints have integration tests
- [ ] No secrets in code or committed config files
- [ ] No magic strings or magic numbers — all extracted to constants/enums (see §2.6)
- [ ] Namespaces are flat at layer level — no folder names beyond Application/Domain/Infrastructure (see §2.7)
- [ ] All date/time fields use `DateTimeOffset`, never `DateTime` — display formatting uses `DateTimeOffsetExtensions` (see §2.8)
- [ ] All logging uses `[LoggerMessage]` delegates — no direct `_logger.LogXxx(...)` or `_logger.Info(...)` calls in new code (see §9, ADR-014)
- [ ] Each logging class has `partial` keyword and a nested `private static partial class Log` (see §9.2)
- [ ] EventIds are unique within their module block and follow the sub-allocation convention (see §9.5)
- [ ] No PII in log messages — IDs only, no email/name/address (see §9.4)
- [ ] API entrypoints (Controllers/PageModels/Handlers) inject `IAppLogger<T>` and emit at minimum: InfoStart + InfoSuccess/WarnXxx (see §9.2)
- [ ] No hard-coded hex/rgb/hsl in component CSS or inline styles — use `var(--*)` tokens (see §7.1)
- [ ] Repeated visual values (radius, shadow, transition) extracted to CSS variables (see §7.3)
- [ ] `AppConstants.Colors` stays in sync with `input.css` `@theme` tokens (see §7.5)

---

## 12. Test-Driven Development (TDD) Policy

This project requires a test-first approach for business logic and feature work. The full policy and workflow are documented in `docs/test-driven-design.md` — follow that guide for new features, bug fixes that affect business rules, and any change that affects domain invariants.

Summary of the rule:

- All new or changed business behavior MUST be accompanied by automated tests written before implementation (write the failing test first). "Business behavior" includes domain methods, application handlers, validation that implements business rules, and any calculation/decision logic that affects users.
- Unit tests are preferred for business rules. Integration tests are required for API endpoints, database interactions, and cross-module flows. Acceptance/business tests (higher-level) are recommended for complex flows.
- Tests must be added to the appropriate test project under `tests/` using existing conventions (e.g., `tests/MarketNest.UnitTests/{Module}/{Feature}Tests.cs`).
- CI will run the test suite and must pass before merging. PRs missing the test-first evidence (test added before or alongside implementation) may be rejected; reviewers should look for failing-first commits or an explicit note in the PR describing the TDD approach taken.

Enforcement:

- Add a PR checklist item (already present) verifying tests were written. Maintainers should confirm tests demonstrate the failing-first workflow when feasible.
- CI pipelines must run unit and integration tests. Consider enforcing coverage gates for critical modules in the future.
- Automated agents and code-review helpers should flag features that introduce business logic without accompanying tests.

See `docs/test-driven-design.md` for the detailed workflow, examples, naming conventions, and PR checklist templates.

