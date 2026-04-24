# MarketNest — Backend Requirements

> Version: 0.1 (Planning) | Status: Draft | Date: 2026-04

---

## 1. Technology Stack

| Technology | Version | Role |
|------------|---------|------|
| **.NET** | 10 LTS | Runtime |
| **ASP.NET Core** | 10 | Web framework (Razor Pages + minimal API) |
| **Entity Framework Core** | 10 | ORM for PostgreSQL |
| **MediatR** | 12.x | CQRS mediator + in-process events |
| **FluentValidation** | 11.x | Request/command validation |
| **MassTransit** | 8.x | Message bus abstraction (RabbitMQ) |
| **StackExchange.Redis** | 2.x | Redis client |
| **Serilog** | 4.x | Structured logging |
| **OpenTelemetry** | 1.x | Distributed tracing + metrics |
| **Testcontainers** | 3.x | Integration test containers |
| **xUnit + FluentAssertions** | Latest | Unit + integration tests |

---

## 2. Solution Structure (Clean Architecture)

```
MarketNest.sln
├── src/
│   ├── MarketNest.Core/                    ← Domain + Shared Kernel
│   │   ├── Common/
│   │   │   ├── Entity.cs                   ← Base entity (Id, DomainEvents)
│   │   │   ├── AggregateRoot.cs
│   │   │   ├── ValueObject.cs
│   │   │   ├── DomainEvent.cs
│   │   │   └── Result.cs                   ← Result<T, Error> pattern
│   │   └── Exceptions/
│   │       ├── DomainException.cs
│   │       └── NotFoundException.cs
│   │
│   ├── MarketNest.Identity/
│   │   ├── Domain/                         ← User, Role, RefreshToken
│   │   ├── Application/                    ← Commands: Register, Login, RefreshToken
│   │   └── Infrastructure/                 ← ASP.NET Core Identity, JWT service
│   │
│   ├── MarketNest.Catalog/
│   │   ├── Domain/                         ← Storefront, Product, ProductVariant, InventoryItem
│   │   ├── Application/                    ← CQRS commands + queries
│   │   └── Infrastructure/                 ← EF Core repositories
│   │
│   ├── MarketNest.Cart/
│   │   ├── Domain/                         ← Cart, CartItem
│   │   ├── Application/                    ← AddToCart, RemoveFromCart, CheckoutCart
│   │   └── Infrastructure/                 ← Redis reservation service
│   │
│   ├── MarketNest.Orders/
│   │   ├── Domain/                         ← Order, OrderLine, Fulfillment, Shipment
│   │   │   └── OrderStateMachine.cs        ← Explicit state transitions
│   │   ├── Application/
│   │   └── Infrastructure/
│   │
│   ├── MarketNest.Payments/
│   │   ├── Domain/                         ← Payment, Payout, Commission
│   │   ├── Application/
│   │   └── Infrastructure/                 ← IPaymentGateway (stub in Phase 1)
│   │
│   ├── MarketNest.Reviews/
│   │   ├── Domain/                         ← Review, ReviewVote
│   │   ├── Application/                    ← CreateReview (with gate check)
│   │   └── Infrastructure/
│   │
│   ├── MarketNest.Disputes/
│   │   ├── Domain/                         ← Dispute, DisputeMessage, Resolution
│   │   ├── Application/
│   │   └── Infrastructure/
│   │
│   ├── MarketNest.Notifications/           ← Phase 1: in-process; Phase 3: separate service
│   │   ├── Domain/                         ← NotificationTemplate, NotificationLog
│   │   ├── Application/                    ← INotificationService handlers
│   │   └── Infrastructure/                 ← MailKit SMTP, SMS stub
│   │
│   └── MarketNest.Web/                     ← ASP.NET Core host (Razor Pages + API)
│       ├── Pages/                          ← Razor Pages
│       ├── Controllers/                    ← Minimal API endpoints (optional)
│       ├── Middleware/                     ← Exception handling, correlation ID
│       └── Program.cs                      ← Composition root
│
└── tests/
    ├── MarketNest.UnitTests/               ← Domain logic, application handlers
    ├── MarketNest.IntegrationTests/        ← Testcontainers, WebApplicationFactory
    └── MarketNest.ArchitectureTests/       ← NetArchTest: enforce layer rules
```

---

## 3. CQRS Pattern

### Command (Write)
```csharp
// Command: explicit intent, always returns Result<T, Error>
public record PlaceOrderCommand(
    Guid BuyerId,
    Guid CartId,
    ShippingAddressDto ShippingAddress,
    string PaymentMethod
) : ICommand<PlaceOrderResult>;

// Handler
public class PlaceOrderCommandHandler : ICommandHandler<PlaceOrderCommand, PlaceOrderResult>
{
    public async Task<Result<PlaceOrderResult, Error>> Handle(
        PlaceOrderCommand command, CancellationToken ct)
    {
        // 1. Validate cart still reserved
        // 2. Create Order aggregate
        // 3. Reserve inventory (confirm from cart reservation)
        // 4. Process payment (stub)
        // 5. Publish OrderPlaced domain event
        // 6. Return order confirmation
    }
}
```

### Query (Read)
```csharp
// Query: read-only, can use raw SQL / Dapper for performance
public record GetOrderDetailQuery(Guid OrderId, Guid RequestingUserId) : IQuery<OrderDetailDto>;

// No EF tracking for queries
public class GetOrderDetailQueryHandler : IQueryHandler<GetOrderDetailQuery, OrderDetailDto>
{
    // Use DbContext.Database.SqlQueryRaw or EF with AsNoTracking()
}
```

---

## 4. Domain Event Pattern

### In Monolith (Phase 1–2): In-process via MediatR
```csharp
// Domain event raised by aggregate
public record OrderPlacedEvent(Guid OrderId, Guid BuyerId, Guid SellerId, decimal Total) 
    : IDomainEvent;

// Aggregate raises event
public class Order : AggregateRoot
{
    public void Place()
    {
        // ... business logic ...
        AddDomainEvent(new OrderPlacedEvent(Id, BuyerId, SellerId, Total));
    }
}

// Dispatcher in SaveChanges
// SaveChangesInterceptor dispatches domain events AFTER commit
```

### Phase 3+: Outbox Pattern for Reliability
```csharp
// Outbox: save event to DB in same transaction as aggregate change
// MassTransit Outbox integration with EF Core
// Background job polls outbox → publishes to RabbitMQ
```

---

## 5. Authentication & Authorization

### JWT Configuration
```
Access Token:   15 min expiry, signed with RS256 (asymmetric)
Refresh Token:  7 days expiry, stored in Redis, HttpOnly cookie
Refresh Flow:   POST /auth/refresh → validate Redis entry → issue new pair
Revocation:     DELETE Redis key on logout; blacklist on password change
```

### Role-Based Policies
```csharp
// Policy definitions in Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SellerOnly", p => p.RequireRole("Seller"));
    options.AddPolicy("AdminOnly",  p => p.RequireRole("Admin"));
    options.AddPolicy("BuyerOrSeller", p => p.RequireRole("Buyer", "Seller"));
    
    // Resource-based: Seller can only edit own products
    options.AddPolicy("OwnStorefront", p => 
        p.Requirements.Add(new OwnStorefrontRequirement()));
});
```

### Resource-Based Authorization Handler
```csharp
public class OwnStorefrontHandler : AuthorizationHandler<OwnStorefrontRequirement, Storefront>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnStorefrontRequirement requirement,
        Storefront resource)
    {
        var userId = context.User.GetUserId();
        if (resource.SellerId == userId)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
```

---

## 6. Data Access Layer

### EF Core Configuration
```csharp
// Each module registers its own DbContext extension
// All modules share one DbContext in monolith (different schemas)
public class MarketNestDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("public");
        
        // Each module applies its own configuration
        builder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContextExtensions).Assembly);
        builder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContextExtensions).Assembly);
        // ...
    }
}

// Example entity configuration (Catalog module)
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", "catalog");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Title).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Price).HasPrecision(18, 2);
        builder.HasQueryFilter(p => !p.IsDeleted); // soft delete
        
        builder.HasMany(p => p.Variants)
               .WithOne(v => v.Product)
               .HasForeignKey(v => v.ProductId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
```

### Repository Pattern (Thin wrapper)
```csharp
// Only used for aggregates — queries bypass repository for performance
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Add(Order order);
    void Update(Order order);  // EF tracks changes automatically
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
```

---

## 7. Redis Integration

### Cart Reservation Service
```csharp
public class RedisCartReservationService : ICartReservationService
{
    private const int ReservationTtlSeconds = 900; // 15 min
    
    public async Task<bool> ReserveAsync(Guid userId, Guid variantId, int qty)
    {
        var key = $"marketnest:cart:{userId}:reservation:{variantId}";
        // Lua script for atomicity: check + set
        var script = @"
            local current = redis.call('GET', KEYS[1])
            if current then
                redis.call('SET', KEYS[1], ARGV[1], 'EX', ARGV[2])
                return 1
            else
                return redis.call('SET', KEYS[1], ARGV[1], 'EX', ARGV[2], 'NX')
            end";
        // ...
    }
    
    public async Task RenewAsync(Guid userId, Guid variantId)
    {
        var key = $"marketnest:cart:{userId}:reservation:{variantId}";
        await _redis.KeyExpireAsync(key, TimeSpan.FromSeconds(ReservationTtlSeconds));
    }
}
```

---

## 8. Background Jobs

Use **Hangfire** (Phase 1-2) or **Quartz.NET** for scheduled tasks:

| Job | Schedule | Description |
|-----|----------|-------------|
| `CleanupExpiredReservations` | Every 5 min | Release DB reservations where Redis key expired |
| `AutoConfirmShippedOrders` | Daily 01:00 | Move SHIPPED orders older than 30 days to DELIVERED |
| `AutoCompleteOrders` | Daily 01:05 | Complete DELIVERED orders with no dispute after 3 days |
| `AutoCancelUnconfirmedOrders` | Every 30 min | Cancel CONFIRMED orders where Seller hasn't acted in 48h |
| `ProcessPayoutBatch` | Daily 02:00 | Calculate and record payouts for COMPLETED orders |
| `SendNotificationDigests` | Daily 08:00 | Review notification digest emails to Sellers |

---

## 9. API Design (Minimal APIs — Phase 2+)

While Razor Pages handles SSR, minimal APIs serve:
- Mobile app future compatibility
- Health check endpoints
- Webhook receivers (payment gateway Phase 2)

```csharp
// Health checks
app.MapHealthChecks("/health", new HealthCheckOptions { ... });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { 
    Predicate = check => check.Tags.Contains("ready") 
});

// Example minimal API endpoint
app.MapPost("/api/cart/items", async (
    AddToCartRequest request,
    IMediator mediator,
    ClaimsPrincipal user) =>
{
    var command = new AddToCartCommand(user.GetUserId(), request.VariantId, request.Quantity);
    var result = await mediator.Send(command);
    return result.Match(
        success => Results.Ok(success),
        error => Results.BadRequest(error)
    );
}).RequireAuthorization();
```

---

## 10. Error Handling Strategy

### Result Pattern (no exceptions for business failures)
```csharp
public record Error(string Code, string Message, ErrorType Type);

public enum ErrorType { Validation, NotFound, Conflict, Unauthorized, Unexpected }

public class Result<TValue, TError>
{
    public bool IsSuccess { get; }
    public TValue? Value { get; }
    public TError? Error { get; }
    
    public TResult Match<TResult>(
        Func<TValue, TResult> onSuccess,
        Func<TError, TResult> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}
```

### Global Exception Handler (unexpected exceptions only)
```csharp
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        // Log with correlation ID
        // Return Problem Details (RFC 7807)
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new ProblemDetails { ... });
    });
});
```

---

## 11. Security Middleware Pipeline

```csharp
// Program.cs — Middleware order matters
app.UseHttpsRedirection();
app.UseHsts();
app.UseStaticFiles(); // before auth — no auth for static files
app.UseCors();        // before routing
app.UseRateLimiter(); // before auth — rate limit unauthenticated too
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapRazorPages();
app.MapHealthChecks("/health");
```

### Rate Limiting Configuration
```csharp
builder.Services.AddRateLimiter(options =>
{
    // Fixed window: 60 requests per minute per IP for public endpoints
    options.AddFixedWindowLimiter("public", config =>
    {
        config.PermitLimit = 60;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 5;
    });
    
    // Strict: 5 requests per 15 min for auth endpoints (brute force protection)
    options.AddFixedWindowLimiter("auth", config =>
    {
        config.PermitLimit = 5;
        config.Window = TimeSpan.FromMinutes(15);
    });
    
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});
```

---

## 12. Testing Requirements

### Unit Tests: Domain Layer Focus
```csharp
// Domain logic must be testable without infrastructure
[Fact]
public void Order_CannotTransitionToShipped_WhenNotConfirmed()
{
    var order = Order.Create(buyerId, cartSnapshot);
    var result = order.MarkAsShipped("TRACK123");
    result.IsFailure.Should().BeTrue();
    result.Error.Code.Should().Be("ORDER_INVALID_TRANSITION");
}
```

### Integration Tests: Testcontainers
```csharp
public class OrderApiTests : IClassFixture<MarketNestWebAppFactory>
{
    // Factory spins up PostgreSQL + Redis containers
    // Real EF Core migrations applied
    // Full HTTP stack tested
}
```

### Architecture Tests: NetArchTest
```csharp
[Fact]
public void DomainLayer_ShouldNotDependOn_Infrastructure()
{
    Types.InAssembly(typeof(Order).Assembly)
         .Should().NotHaveDependencyOn("MarketNest.Infrastructure")
         .GetResult().IsSuccessful.Should().BeTrue();
}
```
