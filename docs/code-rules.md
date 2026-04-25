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

### 2.1 Naming
```csharp
// ✅ DO
public class PlaceOrderCommand { }           // Commands: verb + noun + Command
public class PlaceOrderCommandHandler { }    // Handlers: same + Handler
public record OrderPlacedEvent { }           // Events: past tense + Event
public interface IOrderRepository { }        // Interfaces: I-prefix
public class OrderRepository : IOrderRepository { } // Implementations: no suffix

// ❌ DON'T
public class OrderManager { }                // Vague manager/helper/utils classes
public class OrderService { }                // "Service" suffix: too generic
public class HandleOrder { }                 // Inconsistent naming
```

### 2.2 C# Features to Use
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

### 2.3 Async Rules
```csharp
// ✅ Always propagate CancellationToken
public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default) { }

// ✅ ConfigureAwait(false) in library/infrastructure code (not in PageModels)
var result = await db.SaveChangesAsync(ct).ConfigureAwait(false);

// ❌ Never block async code
var result = GetOrderAsync().Result;   // DEADLOCK risk
var result = GetOrderAsync().GetAwaiter().GetResult(); // same
Task.Run(() => GetOrderAsync()).Wait(); // worse
```

### 2.4 Dependency Injection
```csharp
// ✅ Register by interface, inject by interface
services.AddScoped<IOrderRepository, OrderRepository>();

// ✅ Use primary constructor injection
public class PlaceOrderCommandHandler(
    IOrderRepository orders,
    ICartReservationService reservations,
    IPublisher publisher) : ICommandHandler<PlaceOrderCommand, PlaceOrderResult>
{ }

// ❌ Never use service locator
var repo = serviceProvider.GetService<IOrderRepository>(); // anti-pattern
```

### 2.5 No Magic Strings / Magic Numbers
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

---

## 3. Domain Layer Rules

### 3.1 Aggregate Integrity
```csharp
// ✅ Aggregates own their state — no public setters
public class Order : AggregateRoot
{
    public OrderStatus Status { get; private set; }
    public Money Total { get; private set; }

    // All mutations go through explicit methods
    public Result<Unit, Error> MarkAsShipped(string trackingNumber) { ... }
}

// ❌ No anemic domain model
public class Order
{
    public OrderStatus Status { get; set; }  // Anyone can change state — BAD
}
```

### 3.2 Domain Events
```csharp
// ✅ Raise events inside aggregate methods (not in handlers)
public Result<Unit, Error> Complete()
{
    if (Status != OrderStatus.Delivered)
        return Error.InvalidTransition;
    
    Status = OrderStatus.Completed;
    CompletedAt = DateTime.UtcNow;
    
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

### 4.2 Validation
```csharp
// ✅ FluentValidation for all commands (registered as pipeline behavior)
public class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.BuyerId).NotEmpty();
        RuleFor(x => x.CartId).NotEmpty();
        RuleFor(x => x.ShippingAddress.Street).NotEmpty().MaximumLength(200);
    }
}
// Validation happens in MediatR pipeline BEFORE handler — handler never validates
```

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

// ❌ Never .Include() 3+ levels deep — use dedicated queries or projections
```

---

## 6. Web / Razor Pages Rules

### 6.1 Page Handler Conventions
```csharp
// ✅ Thin page model — delegate to MediatR immediately
public class OrderDetailModel(IMediator mediator) : PageModel
{
    public OrderDetailDto Order { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync(Guid orderId)
    {
        var result = await mediator.Send(new GetOrderDetailQuery(orderId, User.GetUserId()));
        if (result is null) return NotFound();
        Order = result;
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(Guid orderId)
    {
        var result = await mediator.Send(new CancelOrderCommand(orderId, User.GetUserId()));
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

## 7. Error Codes Convention

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

## 8. Logging Standards

```csharp
// ✅ Structured logging — use message templates, not string interpolation
_logger.LogInformation("Order {OrderId} placed by buyer {BuyerId} for {Total:C}",
    order.Id, command.BuyerId, order.Total.Amount);

// ❌ String interpolation loses structure
_logger.LogInformation($"Order {order.Id} placed");

// Log levels:
// Debug:    Developer debugging (disabled in prod)
// Info:     Business events (order placed, payment captured)
// Warning:  Handled errors, slow queries, retry attempts
// Error:    Unexpected failures, unhandled exceptions
// Critical: System down, data corruption risk

// Always log correlation ID (added by middleware)
// Never log: passwords, tokens, full credit card numbers, PII beyond user ID
```

---

## 9. Git Commit Conventions

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

## 10. Code Review Checklist (PR Gate)

Before merging:
- [ ] Domain layer has zero infrastructure dependencies (architecture test passes)
- [ ] All commands/queries validated by FluentValidation
- [ ] No raw SQL strings (Dapper queries use parameters)
- [ ] No public setters on aggregates
- [ ] No async void (except event handlers)
- [ ] All CancellationToken parameters propagated
- [ ] New business rules have unit tests
- [ ] New API endpoints have integration tests
- [ ] No secrets in code or committed config files
- [ ] No magic strings or magic numbers — all extracted to constants/enums (see §2.5)
- [ ] Logging uses structured templates (no interpolation)
