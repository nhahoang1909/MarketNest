---
name: dotnet-code-review
description: >
  Review .NET 10 / ASP.NET Core code for MarketNest following Clean Architecture,
  CQRS, MediatR, EF Core, FluentValidation standards. Use this skill when the user wants to:
  review C# code, check layer boundaries, check naming conventions, review command/query handlers,
  check async/await patterns, DI registration, Result pattern, EF Core queries, FluentValidation,
  Razor Page handlers, HTMX responses, or says anything like "review code", "check code",
  "is this code correct", "review handler", "check convention", "review architecture".
  Activate even when the user simply pastes C# code and asks "any issues with this?".
compatibility:
  tools: [bash, read_file, write_file, list_files, grep_search, run_in_terminal]
  agents: [claude-code, gemini-cli, cursor, continue, aider, copilot]
  stack: [.NET 10, ASP.NET Core 10, EF Core 10, MediatR 12, FluentValidation 11]
---

# .NET Code Review Skill — MarketNest

This skill reviews C# code against the conventions, patterns, and architecture rules of the **MarketNest** project (Modular Monolith → Microservices, Clean Architecture, CQRS, DDD).

> Rule sources: `docs/code-rules.md`, `docs/backend-patterns.md`, `docs/backend-infrastructure.md`, `docs/architecture.md`.
> Always read `CLAUDE.md` and `AGENTS.md` at the repo root before reviewing.

---

## Review Process (Mandatory order)

```
Phase 1: SCAN       → Identify code type, layer, module
Phase 2: ANALYZE    → Check against checklists per code type
Phase 3: REPORT     → Report issues categorized by severity
Phase 4: FIX        → Suggest corrected code (before/after)
```

---

## Phase 1: SCAN — Identify Context

Before reviewing, determine:

```
□ Which layer does the file belong to?
  → Domain (Entity, Aggregate, ValueObject, DomainEvent)
  → Application (Command, Query, Handler, Validator, DTO)
  → Infrastructure (Repository, DbContext config, Redis service)
  → Web (PageModel, Middleware, DI registration, HTMX handler)

□ Which module?
  → Identity / Catalog / Cart / Orders / Payments / Reviews / Disputes / Notifications / Admin

□ Specific code type?
  → Command handler / Query handler / Validator / Repository / PageModel / EF Config / DI / Test
```

If reviewing an entire folder, scan:

**PowerShell:**
```powershell
Get-ChildItem -Recurse -Include *.cs |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
  Sort-Object FullName | Select-Object -First 50 FullName

# Quick folder classification
Get-ChildItem -Recurse -Directory |
  Where-Object { $_.FullName -notmatch '\\(obj|bin)\\' } |
  Sort-Object FullName
```

**Bash:**
```bash
find . -name "*.cs" -not -path "*/obj/*" -not -path "*/bin/*" | sort | head -50
find . -type d -not -path "*/obj/*" -not -path "*/bin/*" | sort
```

---

## Phase 2: ANALYZE — Checklists by Code Type

---

### 2.1 Naming Convention (applies to all files)

```csharp
// ✅ CORRECT
public class PlaceOrderCommand { }             // Command: VerbNoun + "Command"
public class PlaceOrderCommandHandler { }      // Handler: Command name + "Handler"
public class PlaceOrderCommandValidator { }    // Validator: Command name + "Validator"
public record OrderPlacedEvent { }             // Domain Event: PastTense + "Event"
public interface IOrderRepository { }          // Interface: I-prefix
public class OrderRepository { }               // Implementation: no "Impl" suffix
public record OrderDetailDto { }               // DTO: EntityName + "Dto" / "ListItemDto" / "DetailDto"
public record GetOrderDetailQuery { }          // Query: "Get" + What + "Query"
public class GetOrderDetailQueryHandler { }    // Query handler: Query name + "Handler"

// ❌ WRONG — flag immediately
public class OrderManager { }                  // "Manager" too vague
public class OrderService { }                  // "Service" — only for application service interfaces
public class HandleOrder { }                   // Does not follow convention
public class OrderRepositoryImpl { }           // "Impl" suffix unnecessary
public class OrderDto { }                      // Too generic — ListItem or Detail?
```

**Private fields:**
```csharp
// ✅ _camelCase (per .editorconfig)
private readonly IOrderRepository _orders;
private readonly ILogger<PlaceOrderCommandHandler> _logger;

// ❌ WRONG
private IOrderRepository orders;      // missing underscore
private IOrderRepository Orders;      // PascalCase for field
private IOrderRepository m_orders;    // m_ prefix not used
```

**Checklist:**
- [ ] Command: `{Verb}{Noun}Command` (e.g., `PlaceOrderCommand`, `CancelOrderCommand`)
- [ ] Handler: Command/Query name + `Handler`
- [ ] Validator: Command name + `Validator`
- [ ] Event: past tense + `Event` (e.g., `OrderPlacedEvent`, `PaymentCapturedEvent`)
- [ ] Interface: `I` prefix (e.g., `IOrderRepository`)
- [ ] DTO: `{Entity}Dto`, `{Entity}ListItemDto`, `{Entity}DetailDto`
- [ ] Private fields: `_camelCase`
- [ ] No `Manager`, `Helper`, `Utils` classes

---

### 2.2 Domain Layer Rules

**Aggregate integrity:**
```csharp
// ✅ CORRECT — private setters, mutation via methods
public class Order : AggregateRoot
{
    public OrderStatus Status { get; private set; }
    public Money Total { get; private set; }
    private readonly List<OrderLine> _lines = [];
    public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();

    public Result<Unit, Error> MarkAsShipped(string trackingNumber)
    {
        if (Status != OrderStatus.Confirmed)
            return Errors.Order.InvalidTransition(Status, OrderStatus.Shipped);

        Status = OrderStatus.Shipped;
        TrackingNumber = trackingNumber;
        ShippedAt = DateTime.UtcNow;

        AddDomainEvent(new OrderShippedEvent(Id, BuyerId, trackingNumber));
        return Result.Success();
    }
}

// ❌ ANEMIC DOMAIN MODEL — flag immediately
public class Order
{
    public OrderStatus Status { get; set; }      // public setter — anyone can change state
    public List<OrderLine> Lines { get; set; }   // mutable collection leak
}
```

**Domain events must be raised inside aggregates, not in handlers:**
```csharp
// ✅ Raise inside aggregate method
public Result<Unit, Error> Complete()
{
    Status = OrderStatus.Completed;
    AddDomainEvent(new OrderCompletedEvent(Id, BuyerId, Total));
    return Result.Success();
}

// ❌ Raised in handler — WRONG
public async Task<Result<Unit, Error>> Handle(CompleteOrderCommand cmd, CancellationToken ct)
{
    order.Status = OrderStatus.Completed;
    await publisher.Publish(new OrderCompletedEvent(order.Id)); // should be in aggregate
}
```

**Domain must not reference Infrastructure:**
```powershell
# Check Domain project for Infrastructure imports
Select-String -Path src/*/Domain/**/*.cs -Pattern 'using Microsoft\.EntityFrameworkCore|using StackExchange\.Redis|using System\.Net\.Http|using MassTransit' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }
# Expected: no output
```

**State machine transitions use switch expression:**
```csharp
// ✅ Explicit transition table
public Result<Unit, Error> Transition(OrderStatus newStatus) =>
    (Status, newStatus) switch
    {
        (OrderStatus.Pending,    OrderStatus.Confirmed)   => Confirm(),
        (OrderStatus.Confirmed,  OrderStatus.Processing)  => Process(),
        (OrderStatus.Processing, OrderStatus.Shipped)     => Ship(),
        _                                                  =>
            Result.Failure<Unit, Error>(Errors.Order.InvalidTransition(Status, newStatus))
    };
```

**Checklist:**
- [ ] No public setters on aggregate properties
- [ ] Collections return `IReadOnlyList<T>` (not `List<T>`)
- [ ] All mutations return `Result<T, Error>` (no exceptions for business failures)
- [ ] Domain events raised inside aggregate methods, not handlers
- [ ] No `using Microsoft.EntityFrameworkCore` in Domain project
- [ ] Value objects are `record` and immutable
- [ ] Errors use `Errors.{Module}.{ErrorName}` convention

---

### 2.3 Application Layer — Command Handler

```csharp
// ✅ CORRECT — standard command handler
public class PlaceOrderCommandHandler(
    IOrderRepository orders,
    ICartReservationService reservations,
    IUnitOfWork uow,
    ILogger<PlaceOrderCommandHandler> logger)
    : ICommandHandler<PlaceOrderCommand, PlaceOrderResult>
{
    public async Task<Result<PlaceOrderResult, Error>> Handle(
        PlaceOrderCommand command, CancellationToken ct)
    {
        // 1. Load aggregate
        var cart = await reservations.GetCartAsync(command.CartId, ct);
        if (cart is null)
            return Errors.Cart.ReservationExpired;

        // 2. Call domain method — domain raises events
        var result = Order.Create(command.BuyerId, cart);
        if (result.IsFailure)
            return result.Error;

        // 3. Persist
        orders.Add(result.Value);
        await uow.SaveChangesAsync(ct);

        // 4. Return
        logger.LogInformation("Order {OrderId} placed by buyer {BuyerId}",
            result.Value.Id, command.BuyerId);

        return new PlaceOrderResult(result.Value.Id);
    }
}
```

**Common mistakes in command handlers:**

```csharp
// ❌ 1. Throwing exception instead of Result
if (cart is null) throw new NotFoundException("Cart not found"); // WRONG

// ❌ 2. Calling SaveChanges directly — should go through UnitOfWork
await dbContext.SaveChangesAsync(ct); // should be uow.SaveChangesAsync(ct)

// ❌ 3. Business logic in handler (belongs in Domain)
if (order.Lines.Count == 0)
    return Error.Validation("Order must have at least one item"); // should be in Order.Create()

// ❌ 4. Raising events in handler
await publisher.Publish(new OrderPlacedEvent(order.Id)); // should be raised in aggregate

// ❌ 5. Not propagating CancellationToken
var cart = await reservations.GetCartAsync(command.CartId); // missing ct

// ❌ 6. Blocking async
var cart = reservations.GetCartAsync(command.CartId).Result; // DEADLOCK risk

// ❌ 7. Validating in handler (belongs in Validator)
if (string.IsNullOrEmpty(command.PaymentMethod))
    return Error.Validation("Payment method required"); // this is the Validator's job
```

**Checklist:**
- [ ] Uses primary constructor injection
- [ ] Returns `Result<T, Error>` (no throwing)
- [ ] No validation — FluentValidation pipeline handles it
- [ ] No direct domain event raising — aggregate handles it
- [ ] SaveChanges via `IUnitOfWork`, not `dbContext.SaveChangesAsync` directly
- [ ] All async calls propagate `CancellationToken ct`
- [ ] Logging uses structured templates (no string interpolation)
- [ ] Handler contains no business logic — delegates to domain

---

### 2.4 Application Layer — Query Handler

```csharp
// ✅ CORRECT — query handler with AsNoTracking + projection
public class GetOrderDetailQueryHandler(MarketNestDbContext db)
    : IQueryHandler<GetOrderDetailQuery, OrderDetailDto?>
{
    public async Task<OrderDetailDto?> Handle(
        GetOrderDetailQuery query, CancellationToken ct)
    {
        return await db.Orders
            .AsNoTracking()                            // ALWAYS for queries
            .Where(o => o.Id == query.OrderId
                     && o.BuyerId == query.RequestingUserId) // ownership check
            .Select(o => new OrderDetailDto(
                o.Id,
                o.Status.ToString(),
                o.Total.Amount,
                o.CreatedAt,
                o.Lines.Select(l => new OrderLineDto(l.ProductTitle, l.Quantity, l.UnitPrice.Amount)).ToList()
            ))
            .FirstOrDefaultAsync(ct);
    }
}
```

**Common mistakes in query handlers:**
```csharp
// ❌ 1. Missing AsNoTracking — EF tracks entities unnecessarily
var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);

// ❌ 2. Loading entity then mapping manually (N+1 risk + unnecessary data)
var order = await db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(...);
return new OrderDetailDto(order.Id, order.Status.ToString(), ...); // manual map

// ❌ 3. Returning domain entity from Application layer
public async Task<Order> Handle(...) // WRONG — must return DTO

// ❌ 4. Include chain too deep (3+ levels)
db.Orders.Include(o => o.Lines).ThenInclude(l => l.Product).ThenInclude(p => p.Category)
// → Use projection (Select) instead of Include chain
```

**Checklist:**
- [ ] Always has `AsNoTracking()` for read queries
- [ ] Uses `Select()` projection instead of `.Include()` + manual map
- [ ] Returns DTO, not domain Entity
- [ ] Checks ownership in Where clause (prevents IDOR)
- [ ] `IQueryHandler` must not call `SaveChanges` — read-only
- [ ] No `.Include()` deeper than 2 levels

---

### 2.5 FluentValidation

```csharp
// ✅ CORRECT
public class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.BuyerId)
            .NotEmpty().WithMessage("BuyerId is required");

        RuleFor(x => x.CartId)
            .NotEmpty().WithMessage("CartId is required");

        RuleFor(x => x.ShippingAddress.Street)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Street address must not exceed 200 characters");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty()
            .Must(m => new[] { "card", "cod", "ewallet" }.Contains(m))
            .WithMessage("Payment method must be one of: card, cod, ewallet");
    }
}
```

**Common FluentValidation mistakes:**
```csharp
// ❌ 1. Validating in Handler (handler must never validate)
public async Task<Result<...>> Handle(...)
{
    if (command.BuyerId == Guid.Empty)
        return Error.Validation("BuyerId required"); // WRONG — Validator's job
}

// ❌ 2. Missing WithMessage
RuleFor(x => x.BuyerId).NotEmpty(); // no WithMessage → auto-generated message is ugly

// ❌ 3. No Validator for a Command
// Every Command must have a paired Validator — even simple ones

// ❌ 4. Calling database in Validator (complex async validator)
// Should be a business rule in domain, not in Validator
```

**Checklist:**
- [ ] Every Command has a paired `{CommandName}Validator`
- [ ] Every rule has `.WithMessage()` with a clear message
- [ ] Validator contains no business logic (complex rules → domain)
- [ ] Validators are registered via auto-discovery (no manual registration)

---

### 2.6 Async / Await Pattern

```csharp
// ✅ CORRECT
public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
    => await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);

// ✅ ConfigureAwait(false) in Infrastructure code
var result = await httpClient.GetAsync(url, ct).ConfigureAwait(false);

// ✅ Propagate CT through entire call chain
public async Task<Result<T, Error>> Handle(TCommand cmd, CancellationToken ct)
{
    var entity = await repo.GetByIdAsync(cmd.Id, ct);      // ✅ ct passed
    await uow.SaveChangesAsync(ct);                         // ✅ ct passed
}
```

**Common async mistakes:**
```csharp
// ❌ 1. Blocking — DEADLOCK risk
var result = GetOrderAsync().Result;
var result = GetOrderAsync().GetAwaiter().GetResult();
Task.Run(() => GetOrderAsync()).Wait();

// ❌ 2. async void (except event handlers)
public async void HandleOrder() { }   // exception swallowed, cannot be awaited

// ❌ 3. Not propagating CancellationToken
public async Task<Order?> GetByIdAsync(Guid id) // missing CancellationToken
    => await db.Orders.FirstOrDefaultAsync(o => o.Id == id); // missing ct

// ❌ 4. Unnecessary Task creation
public async Task<int> GetCountAsync() => await Task.FromResult(42); // drop async/await
public int GetCount() => 42; // ✅ if no I/O

// ❌ 5. Missing await — dangerous fire-and-forget
SendEmailAsync(email); // missing await — exception silently ignored
```

**Checklist:**
- [ ] No `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` (except startup code)
- [ ] No `async void` (except UI event handlers)
- [ ] All async methods have `CancellationToken ct = default` parameter
- [ ] CancellationToken propagated to all I/O calls
- [ ] `ConfigureAwait(false)` in Infrastructure/Library code
- [ ] No `await Task.FromResult(...)` — drop async if no I/O

---

### 2.7 Dependency Injection

```csharp
// ✅ CORRECT — primary constructor injection
public class PlaceOrderCommandHandler(
    IOrderRepository orders,
    IUnitOfWork uow,
    ILogger<PlaceOrderCommandHandler> logger) : ICommandHandler<...>
{ }

// ✅ Register by interface
services.AddScoped<IOrderRepository, OrderRepository>();

// ✅ Lifetime conventions:
// Scoped:    DbContext, Repositories, Application Services, MediatR handlers
// Transient: Validators, lightweight stateless services
// Singleton: Cache wrappers, configuration readers, HttpClient factory

// ✅ Each module has its own DI extension
// Orders/Infrastructure/DependencyInjection.cs
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

**Common DI mistakes:**
```csharp
// ❌ 1. Service Locator anti-pattern
var repo = serviceProvider.GetService<IOrderRepository>(); // WRONG

// ❌ 2. Injecting implementation instead of interface
public class Handler(OrderRepository repo) // WRONG — inject IOrderRepository
{ }

// ❌ 3. Singleton holding Scoped dependency (captive dependency)
services.AddSingleton<IMyCache, MyCache>(); // if MyCache injects DbContext → BUG

// ❌ 4. Registering directly in Program.cs instead of extension method
builder.Services.AddScoped<IOrderRepository, OrderRepository>(); // should be in module DI

// ❌ 5. Traditional constructor injection when primary constructor is available
public class Handler : ICommandHandler<...>
{
    private readonly IOrderRepository _orders;
    public Handler(IOrderRepository orders) // .NET 10: use primary constructor
    {
        _orders = orders;
    }
}
```

**Checklist:**
- [ ] Inject by interface, not implementation
- [ ] Use primary constructor injection (no constructor body assignment)
- [ ] Each module has `DependencyInjection.cs` with `Add{Module}Module()` extension
- [ ] No service locator (`IServiceProvider.GetService` in handlers)
- [ ] Singleton does not inject Scoped dependencies
- [ ] Correct lifetimes: DbContext → Scoped, Validator → Transient

---

### 2.8 Result Pattern

```csharp
// ✅ CORRECT — Result<T, Error> from Core
public Result<Unit, Error> MarkAsShipped(string trackingNumber)
{
    if (Status != OrderStatus.Confirmed)
        return Errors.Order.InvalidTransition(Status, OrderStatus.Shipped);

    Status = OrderStatus.Shipped;
    return Result.Success();
}

// ✅ In handler — propagate Result
var result = order.MarkAsShipped(command.TrackingNumber);
if (result.IsFailure) return result.Error;

// ✅ Error convention: DOMAIN.ENTITY_ERROR
public static class Errors
{
    public static class Order
    {
        public static Error NotFound(Guid id) =>
            new("ORDER.NOT_FOUND", $"Order {id} was not found", ErrorType.NotFound);

        public static Error InvalidTransition(OrderStatus from, OrderStatus to) =>
            new("ORDER.INVALID_TRANSITION", $"Cannot transition from {from} to {to}", ErrorType.Conflict);
    }
}

// ✅ In PageModel — match Result
var result = await mediator.Send(command);
return result.Match(
    success => RedirectToPage("./Detail", new { orderId = success.OrderId }),
    error => error.Type switch
    {
        ErrorType.NotFound   => NotFound(),
        ErrorType.Conflict   => BadRequest(error.Message),
        _                    => StatusCode(500)
    }
);
```

**Common Result pattern mistakes:**
```csharp
// ❌ 1. Throwing exception for business failure
throw new InvalidOperationException("Order already shipped"); // WRONG

// ❌ 2. Returning null instead of Result
public Order? PlaceOrder(...) => null; // use Result.Failure

// ❌ 3. Error code not following convention
new Error("error1", "something wrong") // WRONG — must be "ORDER.NOT_FOUND"

// ❌ 4. Not handling Result in PageModel
var result = await mediator.Send(command);
// If result.IsFailure not checked → silent failure

// ❌ 5. Command handler returning void instead of Result
public async Task Handle(...) { } // WRONG — ICommandHandler must return Result
```

**Checklist:**
- [ ] Domain methods return `Result<T, Error>` (no throwing for business failures)
- [ ] Error codes follow `DOMAIN.ENTITY_ERROR` format
- [ ] Handler propagates Result (no unwrapping and re-throwing)
- [ ] PageModel handles both success and failure paths from Result
- [ ] No `throw new DomainException` directly (use `Errors.X.Y` factory)

---

### 2.9 EF Core Conventions

```csharp
// ✅ AsNoTracking for all read-only queries
var orders = await db.Orders
    .AsNoTracking()
    .Where(o => o.BuyerId == userId)
    .Select(o => new OrderSummaryDto(...))
    .ToListAsync(ct);

// ✅ Do not return IQueryable from Repository
// Repository Interface:
Task<Order?> GetByIdAsync(Guid id, CancellationToken ct);
// NOT: IQueryable<Order> Query();

// ✅ Soft delete via query filter
builder.HasQueryFilter(o => !o.IsDeleted);

// ✅ Schema-per-module
modelBuilder.HasDefaultSchema("orders"); // in OrderDbContext

// ✅ Migration naming convention: YYYYMMDD_HHmm_Description
// 20260410_1430_AddOrderDispute
```

**Common EF Core mistakes:**
```csharp
// ❌ 1. Missing AsNoTracking in query handler
await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct); // missing AsNoTracking

// ❌ 2. Include chain too deep (3+ levels)
db.Orders
  .Include(o => o.Lines)
    .ThenInclude(l => l.Product)
      .ThenInclude(p => p.Category) // 3 levels → use projection

// ❌ 3. Returning IQueryable from Repository (EF leaks into domain)
public IQueryable<Order> GetAll() => db.Orders; // WRONG

// ❌ 4. Raw SQL not parameterized
db.Orders.FromSqlRaw($"SELECT * WHERE id = {id}"); // SQL injection risk
db.Orders.FromSqlRaw("SELECT * WHERE id = {0}", id); // ✅ or use EF LINQ

// ❌ 5. Cross-module direct schema access
// Orders module must not use db.Products directly
// → use cross-module interface or domain event

// ❌ 6. SaveChanges called multiple times in one request
await db.SaveChangesAsync(); // 1st
await db.SaveChangesAsync(); // 2nd — should batch into one via UoW
```

**Checklist:**
- [ ] `AsNoTracking()` on all read queries
- [ ] Repository does not return `IQueryable<T>`
- [ ] No `.Include()` deeper than 2 levels — use `Select()` projection
- [ ] Raw SQL (if any) uses parameterized queries (Dapper with `@param`, no string format)
- [ ] Module does not access another module's schema directly
- [ ] Query filter for soft delete is configured
- [ ] Schema annotation: `modelBuilder.HasDefaultSchema("{module}")`

---

### 2.10 Razor Page / HTMX Handler

```csharp
// ✅ CORRECT — Thin PageModel, delegate immediately to MediatR
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
            error => error.Type switch
            {
                ErrorType.NotFound => NotFound(),
                ErrorType.Conflict => BadRequest(error.Message),
                _ => StatusCode(500)
            }
        );
    }
}

// ✅ HTMX partial response
public async Task<IActionResult> OnGetOrderListAsync(CancellationToken ct)
{
    var orders = await mediator.Send(new GetOrderListQuery(User.GetUserId()), ct);

    if (Request.IsHtmx())
        return Partial("_OrderList", orders);

    return Page();
}

// ✅ HTMX redirect after POST
Response.Headers["HX-Redirect"] = Url.Page("/Orders/Detail", new { orderId });
return Ok();

// ✅ HTMX error response
Response.Headers["HX-Retarget"] = "#error-container";
Response.Headers["HX-Reswap"] = "innerHTML";
return Partial("_Error", new ErrorViewModel(error.Message));
```

**Common PageModel mistakes:**
```csharp
// ❌ 1. Business logic in PageModel
public async Task<IActionResult> OnPostAsync(Guid orderId)
{
    var order = await db.Orders.FindAsync(orderId); // WRONG — calling DB directly
    if (order.Status != OrderStatus.Pending)        // business logic in page
        ModelState.AddModelError("", "Cannot cancel shipped order");
}

// ❌ 2. Calling Repository directly (bypassing MediatR)
public class OrderModel(IOrderRepository repo) : PageModel
{
    public async Task OnGetAsync(Guid id) =>
        Order = await repo.GetByIdAsync(id); // should go through MediatR Query
}

// ❌ 3. Not handling HTMX vs full page request
return Page(); // always returns full page even for HTMX partial requests

// ❌ 4. Not propagating CancellationToken
public async Task<IActionResult> OnGetAsync(Guid orderId)
    => await mediator.Send(new GetOrderDetailQuery(orderId, User.GetUserId())); // missing ct
```

**Checklist:**
- [ ] PageModel injects `IMediator`, not Repository directly
- [ ] No business logic in PageModel
- [ ] `OnGet*` delegates immediately to Query handler
- [ ] `OnPost*` delegates immediately to Command handler, handles Result fully
- [ ] Checks `Request.IsHtmx()` → returns `Partial()` for HTMX requests
- [ ] HTMX redirect uses `HX-Redirect` header instead of `RedirectToPage()`
- [ ] CancellationToken is propagated

---

### 2.11 Logging Convention

```csharp
// ✅ Structured logging — message templates
_logger.LogInformation("Order {OrderId} placed by buyer {BuyerId} for {Total:C}",
    order.Id, command.BuyerId, order.Total.Amount);

_logger.LogWarning("Cart reservation {CartId} expired for buyer {BuyerId}",
    command.CartId, command.BuyerId);

_logger.LogError(ex, "Failed to process payment for order {OrderId}",
    command.OrderId);

// Log level guide:
// Debug:    Developer debugging — disabled in prod
// Info:     Business events (order placed, payment captured, user registered)
// Warning:  Handled errors, slow queries (>500ms), retry attempts
// Error:    Unexpected failure — needs investigation
// Critical: System integrity risk

// NEVER LOG:
// ❌ Passwords, tokens, refresh tokens, API keys
// ❌ Full credit card numbers, CVV
// ❌ PII beyond UserId (email only at Debug level)
```

**Common logging mistakes:**
```csharp
// ❌ 1. String interpolation (loses structured logging)
_logger.LogInformation($"Order {order.Id} placed"); // WRONG

// ❌ 2. Logging sensitive data
_logger.LogInformation("User {Email} logged in with password {Password}",
    user.Email, command.Password); // CRITICAL SECURITY ISSUE

// ❌ 3. Wrong log level
_logger.LogError("User not found"); // should be Warning or Info if expected
_logger.LogInformation("Database connection failed"); // should be Error/Critical
```

---

## Phase 3: REPORT — Report Format

```markdown
# .NET Code Review Report
**File/Module**: `{file or module name}`
**Layer**: {Domain / Application / Infrastructure / Web}
**Date**: {date}

## Summary

| Severity | Count |
|----------|-------|
| 🔴 CRITICAL | X |
| 🟠 HIGH     | X |
| 🟡 MEDIUM   | X |
| 🟢 LOW      | X |

## Issues

### 🔴 CRITICAL — {Issue name}
- **File**: `path/to/File.cs:line`
- **Rule**: {Naming / Domain / Command / Query / Async / DI / Result / EF / HTMX / Logging}
- **Problem**: ...
- **Fix**: (see Phase 4)

### 🟠 HIGH — ...
...

## ✅ Positive Observations
- ...

## Consolidated Checklist
- [ ] Naming convention ✅/❌
- [ ] Layer boundary ✅/❌
- [ ] Async/Await pattern ✅/❌
- [ ] Result pattern ✅/❌
- [ ] DI correctness ✅/❌
- [ ] EF Core best practices ✅/❌
- [ ] Structured logging ✅/❌
```

**Severity guide:**
- 🔴 **CRITICAL**: Layer boundary violation, blocking async (.Result), SQL injection, business logic in PageModel, public setter on aggregate
- 🟠 **HIGH**: Missing CancellationToken, throwing exception instead of Result, anemic domain model, missing validator, missing AsNoTracking
- 🟡 **MEDIUM**: Naming convention violation, missing WithMessage in validator, string interpolation in logging, Include too deep
- 🟢 **LOW**: Style preference, minor naming, missing comment

---

## Phase 4: FIX — Patch Template

```
🔧 FIX: {Issue name}
📁 File: {path}:{line}
🔴 Severity: CRITICAL | HIGH | MEDIUM | LOW
📏 Rule: {rule name}

BEFORE (Problem):
```csharp
{old code}
```

AFTER (Fixed):
```csharp
{new code}
```

✅ Reason: {brief explanation of why the fix is needed}
```

---

## Quick Scan Commands

Use these to quickly scan the entire codebase before detailed review:

**PowerShell:**
```powershell
# 1. Layer boundary violation — Domain referencing Infrastructure
Select-String -Path src/*/Domain/**/*.cs -Pattern 'using Microsoft\.EntityFrameworkCore|using StackExchange\.Redis|using MassTransit' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# 2. Blocking async
Select-String -Path src/**/*.cs -Pattern '\.Result\b|\.Wait\(\)|\.GetAwaiter\(\)\.GetResult\(\)' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# 3. Anemic domain model — public setter
Select-String -Path src/*/Domain/**/*.cs -Pattern '\{ get; set; \}' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# 4. String interpolation in logging
Select-String -Path src/**/*.cs -Pattern 'Log.*\$"' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# 5. Service locator anti-pattern
Select-String -Path src/**/*.cs -Pattern '\.GetService<|\.GetRequiredService<' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' -and $_.Path -notmatch 'Program\.cs|DependencyInjection' }

# 6. async void (outside event handlers)
Select-String -Path src/**/*.cs -Pattern 'async void ' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# 7. Banned class name patterns
Select-String -Path src/**/*.cs -Pattern 'class\s+\w*Manager\b|class\s+\w*Helper\b|class\s+\w*Utils\b' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# 8. Missing CancellationToken in async methods
Select-String -Path src/**/*.cs -Pattern 'public async Task' -Recurse |
  Where-Object { $_.Line -notmatch 'CancellationToken' -and $_.Path -notmatch '\\(bin|obj)\\' }

# 9. Cross-module DB access violation (example: Orders accessing Products)
Select-String -Path src/MarketNest.Orders/**/*.cs -Pattern 'db\.Products|db\.Storefronts|db\.Users' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }
```

**Bash:**
```bash
# 1. Layer boundary violation
grep -rn "using Microsoft.EntityFrameworkCore\|using StackExchange.Redis\|using MassTransit" \
  src/*/Domain/ --include="*.cs"

# 2. Blocking async
grep -rn "\.Result\b\|\.Wait()\|GetAwaiter().GetResult()" \
  src/ --include="*.cs" | grep -v "node_modules\|obj\|bin"

# 3. Anemic domain model
grep -rn "{ get; set; }" src/*/Domain/ --include="*.cs"

# 4-9: similar patterns as PowerShell versions above
```

---

## Important Notes

- **Do not fix anything without confirmation** (unless the user says "just fix it")
- **Understand context before flagging**: some patterns may be intentional (e.g., direct DbContext in query handler is acceptable when no Repository exists for queries)
- **Prioritize CRITICAL and HIGH** — do not report every LOW issue if there are many
- **Explain briefly** — the user needs to understand *why*, not just *what* to fix
- **Suggest concretely** — every issue must have a code example of the fix

