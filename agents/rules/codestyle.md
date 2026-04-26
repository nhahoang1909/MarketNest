# Code Style — C# / .NET Conventions

Source of truth: `docs/code-rules.md`

## Naming

```csharp
// Commands / Queries / Events
PlaceOrderCommand           // verb + noun + Command
PlaceOrderCommandHandler    // same + Handler
GetOrderByIdQuery           // Get + noun + Query
OrderPlacedEvent            // past-tense noun + Event

// Interfaces & implementations
IOrderRepository            // I-prefix
OrderRepository             // no suffix on implementation

// ❌ Never: OrderManager, OrderService, HandleOrder, OrderHelper, OrderUtils
```

## C# Patterns to Use

```csharp
// Records for DTOs, value objects, commands, queries
public record Money(decimal Amount, string Currency);

// Primary constructors for DI
public class OrderRepository(MarketNestDbContext db) : IOrderRepository { }

// Result<T, Error> — NEVER throw for business failures
public Result<Order, Error> PlaceOrder(PlaceOrderCommand cmd) { ... }

// Pattern matching for state machines
(Status, newStatus) switch {
    (Pending, Confirmed)  => Confirm(),
    (Confirmed, Shipped)  => Ship(),
    _                     => Result.Failure(Errors.Order.InvalidTransition(...))
};

// Always initialize collections — no null
private readonly List<OrderLine> _lines = [];
public IReadOnlyList<OrderLine> Lines => _lines.AsReadOnly();
```

## Async Rules

```csharp
// ✅ Always propagate CancellationToken
Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)

// ✅ ConfigureAwait(false) in infrastructure code (not in PageModels)
await db.SaveChangesAsync(ct).ConfigureAwait(false);

// ❌ Never block
GetOrderAsync().Result               // deadlock risk
GetOrderAsync().GetAwaiter().GetResult()
```

## Dependency Injection

```csharp
// Register and inject by interface
services.AddScoped<IOrderRepository, OrderRepository>();

// ❌ Never service locator
serviceProvider.GetService<IOrderRepository>()
```

## Logging

```csharp
// ✅ Structured templates
_logger.LogInformation("Order {OrderId} placed by {BuyerId}", order.Id, cmd.BuyerId);

// ❌ String interpolation (loses structure)
_logger.LogInformation($"Order {order.Id} placed");

// Log levels: Debug=dev, Info=business events, Warning=handled errors, Error=unexpected, Critical=data risk
// Never log: passwords, tokens, credit card numbers, PII beyond user ID
```

## Error Codes

All errors use `DOMAIN.ENTITY_ERROR` format:

```csharp
public static class Errors
{
    public static class Order
    {
        public static Error NotFound(Guid id) =>
            new("ORDER.NOT_FOUND", $"Order {id} not found", ErrorType.NotFound);

        public static Error InvalidTransition(OrderStatus from, OrderStatus to) =>
            new("ORDER.INVALID_TRANSITION", $"Cannot transition {from} → {to}", ErrorType.Conflict);
    }

    public static class Cart
    {
        public static Error InsufficientStock =>
            new("CART.INSUFFICIENT_STOCK", "Not enough stock", ErrorType.Conflict);
    }
}
```
