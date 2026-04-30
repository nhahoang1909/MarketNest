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
// ✅ CORRECT — standard command handler (ADR-027: transaction filter owns CommitAsync)
public partial class PlaceOrderCommandHandler(
    IOrderRepository orders,
    ICartReservationService reservations,
    IRuntimeContext ctx,
    IAppLogger<PlaceOrderCommandHandler> logger)
    : ICommandHandler<PlaceOrderCommand, PlaceOrderResult>
{
    public async Task<Result<PlaceOrderResult, Error>> Handle(
        PlaceOrderCommand command, CancellationToken ct)
    {
        // 1. Require authenticated user via IRuntimeContext (ADR-028)
        var userId = ctx.CurrentUser.RequireId(); // throws UnauthorizedException if anonymous

        // 2. Load aggregate
        var cart = await reservations.GetCartAsync(command.CartId, ct);
        if (cart is null)
            return Errors.Cart.ReservationExpired;

        // 3. Call domain method — domain raises events internally
        var result = Order.Create(userId, cart);
        if (result.IsFailure)
            return result.Error;

        // 4. Persist — DO NOT call uow.CommitAsync() here.
        //    RazorPageTransactionFilter / TransactionActionFilter owns the commit (ADR-027).
        orders.Add(result.Value);
        // DO NOT: await uow.CommitAsync(ct);  ← transaction filter calls this after Handle returns

        // 5. Log via [LoggerMessage] delegate (MN005/MN006/MN007 rules)
        Log.OrderPlaced(logger, result.Value.Id, userId);

        return new PlaceOrderResult(result.Value.Id);
    }

    private static partial class Log
    {
        [LoggerMessage(LogEventId.Orders + 1, LogLevel.Information,
            "Order {OrderId} placed by user {UserId}")]
        public static partial void OrderPlaced(ILogger logger, Guid orderId, Guid userId);
    }
}
```

> **ADR-027 — Transaction ownership rule**: Command handlers running inside the HTTP pipeline
> MUST NOT call `uow.CommitAsync()` or `dbContext.SaveChangesAsync()`. The transaction filter
> (`RazorPageTransactionFilter` / `TransactionActionFilter`) wraps the entire handler and calls
> `CommitAsync` automatically after the handler returns successfully.
> **Exception**: Background jobs (implementing `IBackgroundJob`) run outside the HTTP pipeline
> and MUST call `uow.CommitAsync(ct)` themselves.

**Common mistakes in command handlers:**

```csharp
// ❌ 1. Throwing exception instead of Result
if (cart is null) throw new NotFoundException("Cart not found"); // WRONG

// ❌ 2. Calling SaveChanges directly in an HTTP handler — transaction filter does this
await dbContext.SaveChangesAsync(ct); // WRONG in HTTP context
await uow.CommitAsync(ct);            // WRONG in HTTP context — filter owns the commit

// ❌ 3. Business logic in handler (belongs in Domain)
if (order.Lines.Count == 0)
    return Error.Validation("Order must have at least one item"); // should be in Order.Create()

// ❌ 4. Raising events in handler
await publisher.Publish(new OrderPlacedEvent(order.Id)); // should be raised in aggregate

// ❌ 5. Not propagating CancellationToken
var cart = await reservations.GetCartAsync(command.CartId); // missing ct

// ❌ 6. Blocking async — DEADLOCK risk (MN004)
var cart = reservations.GetCartAsync(command.CartId).Result;

// ❌ 7. Validating in handler (belongs in Validator)
if (string.IsNullOrEmpty(command.PaymentMethod))
    return Error.Validation("Payment method required"); // this is the Validator's job

// ❌ 8. Injecting ILogger<T> instead of IAppLogger<T> (MN007)
public class Handler(ILogger<Handler> logger) { }   // MN007 violation

// ❌ 9. Using ICurrentUserService instead of IRuntimeContext (ADR-028)
public class Handler(ICurrentUserService userSvc) { } // use IRuntimeContext instead

// ❌ 10. Not marking logging class as partial (MN006)
public class Handler(IAppLogger<Handler> logger)     // missing 'partial' keyword → MN006
```

**Checklist:**
- [ ] Class is `partial` (required if it logs)
- [ ] Uses primary constructor injection
- [ ] Inject `IRuntimeContext` (not `ICurrentUserService`)
- [ ] Inject `IAppLogger<T>` (not `ILogger<T>`)
- [ ] `[LoggerMessage]` delegates in nested `private static partial class Log`
- [ ] Returns `Result<T, Error>` (no throwing)
- [ ] No validation — FluentValidation pipeline handles it
- [ ] No direct domain event raising — aggregate handles it
- [ ] No `uow.CommitAsync(ct)` or `dbContext.SaveChangesAsync()` in HTTP handlers
- [ ] All async calls propagate `CancellationToken ct`
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

### 2.11 Logging Convention (ADR-033 + MN005/MN006/MN007)

MarketNest uses source-generated `[LoggerMessage]` delegates. Direct `Log*()` calls violate MN005.

```csharp
// ✅ CORRECT — IAppLogger<T> + [LoggerMessage] + partial class
public partial class PlaceOrderCommandHandler(IAppLogger<PlaceOrderCommandHandler> logger)
{
    public async Task<Result<PlaceOrderResult, Error>> Handle(
        PlaceOrderCommand cmd, CancellationToken ct)
    {
        Log.OrderPlaced(logger, cmd.OrderId, cmd.BuyerId);
        // ...
    }

    private static partial class Log
    {
        // EventId: module block + sequential index. Orders = 40000–49999 (example)
        [LoggerMessage(40001, LogLevel.Information, "Order {OrderId} placed by buyer {BuyerId}")]
        public static partial void OrderPlaced(ILogger logger, Guid orderId, Guid buyerId);

        [LoggerMessage(40002, LogLevel.Warning, "Cart reservation {CartId} expired for buyer {BuyerId}")]
        public static partial void CartReservationExpired(ILogger logger, Guid cartId, Guid buyerId);

        [LoggerMessage(40003, LogLevel.Error, "Payment failed for order {OrderId}")]
        public static partial void PaymentFailed(ILogger logger, Guid orderId, Exception ex);
    }
}
```

**Log level guide:**
- `Debug`: Developer debugging — disabled in prod
- `Info`: Business events (order placed, payment captured, user registered)
- `Warning`: Handled errors, slow queries (>500ms), retry attempts
- `Error`: Unexpected failure — needs investigation
- `Critical`: System integrity risk

**NEVER LOG:**
- Passwords, tokens, refresh tokens, API keys
- Full credit card numbers, CVV
- PII beyond UserId (email only at Debug level)

**Common logging mistakes:**
```csharp
// ❌ 1. Direct logger call — MN005 violation
_logger.LogInformation("Order {OrderId} placed", orderId);   // WRONG

// ❌ 2. String interpolation — loses structured logging
_logger.LogInformation($"Order {orderId} placed"); // WRONG

// ❌ 3. Class not partial — MN006 violation
public class Handler(IAppLogger<Handler> logger) { }  // missing 'partial' → MN006

// ❌ 4. Injecting ILogger<T> instead of IAppLogger<T> — MN007 violation
public class Handler(ILogger<Handler> logger) { }  // wrong interface

// ❌ 5. Logging sensitive data
Log.UserLoggedIn(logger, user.Email, command.Password); // CRITICAL: never log passwords
```

**Checklist:**
- [ ] Class that logs is `partial` (required by Roslyn — MN006)
- [ ] Inject `IAppLogger<T>`, not `ILogger<T>` (MN007)
- [ ] All log calls are via `[LoggerMessage]` delegates in `private static partial class Log`
- [ ] Each `[LoggerMessage]` uses the module's reserved EventId block
- [ ] No string interpolation in log calls
- [ ] No sensitive data (passwords, tokens, CVV) in any log call

---

### 2.12 IRuntimeContext (ADR-028)

Use `IRuntimeContext` instead of `ICurrentUserService` or ad-hoc `HttpContext` access.

```csharp
// ✅ CORRECT — inject IRuntimeContext in command handlers
public partial class PlaceOrderCommandHandler(
    IOrderRepository orders,
    IRuntimeContext ctx)
    : ICommandHandler<PlaceOrderCommand, PlaceOrderResult>
{
    public async Task<Result<PlaceOrderResult, Error>> Handle(
        PlaceOrderCommand command, CancellationToken ct)
    {
        // RequireId() throws UnauthorizedException if user is not authenticated
        var userId = ctx.CurrentUser.RequireId();

        // For audit interceptors / logging that must never throw:
        var userIdOrNull = ctx.CurrentUser.IdOrNull; // null if anonymous

        var correlationId = ctx.CorrelationId; // trace header value
        // ...
    }
}

// ✅ Background jobs: use BackgroundJobRuntimeContext
public class ExpireSalesJob(IUnitOfWork uow) : IBackgroundJob
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var ctx = BackgroundJobRuntimeContext.ForSystemJob("catalog.variant.expire-sales");
        // use ctx.CurrentUser.IdOrNull (always null for system jobs)
    }
}

// ✅ Tests: use TestRuntimeContext
var ctx = TestRuntimeContext.AsSeller(sellerId: Guid.NewGuid());
var ctx = TestRuntimeContext.AsAdmin();
var ctx = TestRuntimeContext.Anonymous();
```

**Common mistakes:**
```csharp
// ❌ Using ICurrentUserService (removed in ADR-028)
public class Handler(ICurrentUserService userSvc) { } // WRONG

// ❌ Direct HttpContext access in Application layer
public class Handler(IHttpContextAccessor http) { }   // WRONG — Application layer cannot know HTTP

// ❌ RequireId() in audit interceptors — throws for anonymous users
ctx.CurrentUser.RequireId(); // in EF interceptor → use IdOrNull instead
```

**Checklist:**
- [ ] Write handlers inject `IRuntimeContext` (not `ICurrentUserService`)
- [ ] Write handlers use `ctx.CurrentUser.RequireId()` to enforce authentication
- [ ] Audit interceptors / read handlers use `ctx.CurrentUser.IdOrNull`
- [ ] Background jobs use `BackgroundJobRuntimeContext.ForSystemJob(jobKey)`
- [ ] Tests use `TestRuntimeContext.AsSeller()`, `.AsAdmin()`, or `.Anonymous()`

---

### 2.13 Frontend / Razor Pages Conventions (ADR-035, ADR-030)

```cshtml
@* ✅ SharedViewPaths constants — never inline ~/Pages/Shared/... strings (ADR-035) *@
<partial name="@SharedViewPaths.TextField" model="..." />
<partial name="@SharedViewPaths.FormActions" model="..." />

@* ❌ WRONG — magic string violates ADR-035 *@
<partial name="~/Pages/Shared/Forms/_TextField.cshtml" model="..." />
```

```cshtml
@* ✅ FieldLimits constants in maxlength/min/max attributes *@
<input type="text" maxlength="@FieldLimits.ProductTitle.Max" />

@* ❌ WRONG — magic number *@
<input type="text" maxlength="200" />
```

```csharp
// ✅ ValidationMessages for error text — no inline strings
RuleFor(x => x.Title)
    .MaximumLength(FieldLimits.ProductTitle.Max)
    .WithMessage(ValidationMessages.MaxLength("Title", FieldLimits.ProductTitle.Max));

// ❌ WRONG — inline string literal
RuleFor(x => x.Title)
    .MaximumLength(200)
    .WithMessage("Title must not exceed 200 characters");  // should use ValidationMessages
```

**Checklist:**
- [ ] All `<partial name="...">` use `SharedViewPaths.*` constants
- [ ] All `Html.PartialAsync(...)` calls use `SharedViewPaths.*` constants
- [ ] Input `maxlength`, `min`, `max` attributes reference `FieldLimits.*` constants
- [ ] Validator error messages use `ValidationMessages.*` factory methods
- [ ] AppRoutes used for all links / redirects (no magic URL strings)

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

# 2. Blocking async (MN004)
Select-String -Path src/**/*.cs -Pattern '\.Result\b|\.Wait\(\)|\.GetAwaiter\(\)\.GetResult\(\)' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# 3. Anemic domain model — public setter (MN016)
Select-String -Path src/*/Domain/**/*.cs -Pattern '\{ get; set; \}' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# 4. Direct logger call in handlers — should use [LoggerMessage] (MN005)
Select-String -Path src/**/*.cs -Pattern '_logger\.Log|logger\.Log' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# 5. ILogger<T> injection — should be IAppLogger<T> (MN007)
Select-String -Path src/**/*.cs -Pattern 'ILogger<' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\|AppLogger' }

# 6. uow.CommitAsync / SaveChangesAsync in HTTP handlers (ADR-027 violation)
Select-String -Path src/**/*.cs -Pattern 'CommitAsync|SaveChangesAsync' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\|Web\\|Program|UnitOfWork|BackgroundJob' }

# 7. Service locator anti-pattern (MN010)
Select-String -Path src/**/*.cs -Pattern '\.GetService<|\.GetRequiredService<' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' -and $_.Path -notmatch 'Program\.cs|DependencyInjection' }

# 8. async void (MN003)
Select-String -Path src/**/*.cs -Pattern 'async void ' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# 9. Banned class suffixes (MN002)
Select-String -Path src/**/*.cs -Pattern 'class\s+\w*Manager\b|class\s+\w*Helper\b|class\s+\w*Utils\b' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# 10. Missing CancellationToken on public async methods (MN011)
Select-String -Path src/**/*.cs -Pattern 'public async Task' -Recurse |
  Where-Object { $_.Line -notmatch 'CancellationToken' -and $_.Path -notmatch '\\(bin|obj)\\' }

# 11. Cross-module DB access violation (example: Orders accessing Products)
Select-String -Path src/MarketNest.Orders/**/*.cs -Pattern 'db\.Products|db\.Storefronts|db\.Users' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# 12. Inline ~/Pages/Shared magic strings (ADR-035 violation)
Select-String -Path src/**/*.cshtml -Pattern '~/Pages/Shared/' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\' }

# 13. Flat namespace violation (MN008) — sub-folder in namespace
Select-String -Path src/**/*.cs -Pattern 'namespace MarketNest\.\w+\.\w+\.\w+' -Recurse |
  Where-Object { $_.Path -notmatch '\\(bin|obj)\\|MarketNest\.Web' }
```

---

## Important Notes

- **Do not fix anything without confirmation** (unless the user says "just fix it")
- **Understand context before flagging**: some patterns may be intentional (e.g., direct DbContext in query handler is acceptable when no Repository exists for queries)
- **Prioritize CRITICAL and HIGH** — do not report every LOW issue if there are many
- **Explain briefly** — the user needs to understand *why*, not just *what* to fix
- **Suggest concretely** — every issue must have a code example of the fix

