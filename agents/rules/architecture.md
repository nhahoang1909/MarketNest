# Architecture Rules

Source of truth: `docs/architecture-requirements.md`, `docs/contract-first-guide.md`, `docs/code-rules.md`

## Phase Awareness

Before writing any code, confirm the current phase:
- **Phase 1**: Modular monolith — in-process MediatR, one PostgreSQL DB, schema-per-module
- **Phase 3**: RabbitMQ + MassTransit, YARP gateway, Notification Service extracted
- **Phase 4**: Kubernetes, Helm, ArgoCD

Do not introduce Phase 3+ infrastructure (RabbitMQ consumers, YARP config, gRPC) in Phase 1 code.

## Module Boundaries — Strict

```
❌ NEVER: One module references another module's concrete class or DB schema
✅ Sync cross-module calls: via interface defined in MarketNest.Core/Contracts/
✅ Async cross-module calls: domain events via IPublisher (Phase 1) / RabbitMQ (Phase 3+)
```

PostgreSQL schemas enforce this physically:
```
identity.*  catalog.*  orders.*  payments.*  reviews.*  disputes.*
```

No SELECT across schema boundaries — use a service interface or event.

## Layer Rules (enforced by NetArchTest)

```
Domain     → depends on: System, MarketNest.Core only
Application → depends on: Domain, Core
Infrastructure → depends on: Application, Domain, EF Core, Redis, etc.
Web (Razor Pages) → depends on: Application, Infrastructure (for DI only)

❌ Domain must never reference: EF Core, Redis, HttpClient, or any NuGet infrastructure package
❌ Web layer must never call repositories directly — always go through MediatR
```

## Domain Layer

```csharp
// Aggregates own their state — private setters only
public class Order : AggregateRoot
{
    public OrderStatus Status { get; private set; }

    // All mutations through explicit methods that return Result
    public Result<Unit, Error> MarkAsShipped(string trackingNumber) { ... }
}

// ❌ Anemic domain model — public setters = wrong
public class Order { public OrderStatus Status { get; set; } }

// Domain events raised INSIDE aggregate methods, not in handlers
AddDomainEvent(new OrderCompletedEvent(Id, BuyerId, SellerId, Total));
```

## Application Layer — CQRS

```csharp
// Commands: change state, always return Result<T, Error>
public interface ICommandHandler<TCommand, TResult>
    where TCommand : ICommand<TResult>

// Queries: read-only, NEVER change state, no Result wrapper needed
public interface IQueryHandler<TQuery, TResult>
    where TQuery : IQuery<TResult>

// ❌ Don't mix: a query that inserts, a command that returns a list
```

Validation happens in MediatR pipeline via FluentValidation — handlers never manually validate.

## Infrastructure Layer — EF Core

```csharp
// ✅ AsNoTracking() for all queries
db.Orders.AsNoTracking().Where(...).Select(o => new OrderSummaryDto(...))

// ✅ Soft delete via query filter
builder.HasQueryFilter(o => !o.IsDeleted);

// ❌ No raw SQL strings — EF parameterized queries only
//    Exception: complex reporting with Dapper (still parameterized)
// ❌ No IQueryable returned from repositories (leaks EF into domain)
// ❌ No .Include() 3+ levels deep — use dedicated queries or projections
// ❌ Repositories only for aggregates, not every entity
```

## Web Layer — Razor Pages

```csharp
// Thin PageModel — delegate to MediatR immediately
public class OrderDetailModel(IMediator mediator) : PageModel
{
    public async Task<IActionResult> OnGetAsync(Guid orderId)
    {
        var result = await mediator.Send(new GetOrderDetailQuery(orderId, User.GetUserId()));
        if (result is null) return NotFound();
        Order = result;
        return Page();
    }
}

// HTMX partial responses
if (Request.IsHtmx()) return Partial("_OrderList", model);
return Page();
```

## Contract-First Rule

When adding a new module or feature, define ALL of these before any implementation:
1. Aggregates, value objects, domain events
2. Commands and queries (one per use case / screen)
3. Validators (paired to every command)
4. DTOs (per query response)
5. Cross-module service interfaces (in `MarketNest.Core/Contracts/`)
6. Repository interface
7. Redis cache keys (in `CacheKeys.cs`)
8. Pages/routes and HTMX partials

See `docs/contract-first-guide.md` Section 7 for the full checklist.
