---
name: architecture-guard
description: >
  Scan the entire MarketNest codebase to enforce Clean Architecture rules: Domain must not
  reference Infrastructure, no cross-module DB access, modules can only communicate via
  events/interfaces, DDD aggregate boundaries must be correct. Use this skill when the user
  wants to: check architecture, enforce layer rules, find dependency violations, check DDD
  boundaries, review aggregate design, check module isolation, or says anything like
  "architecture review", "check layer", "enforce rules", "DDD boundary", "module isolation",
  "aggregate design", "clean architecture", "dependency violation", "cross-module".
  Activate when the user uploads .cs, .csproj files or asks about module structure.
compatibility:
  tools: [bash, read_file, write_file, list_files, grep_search, run_in_terminal]
  agents: [claude-code, gemini-cli, cursor, continue, aider, copilot]
  stack: [.NET 10, Clean Architecture, DDD, MediatR 12, NetArchTest]
---

# Architecture Guard Skill — MarketNest

This skill enforces all architectural rules of MarketNest: layer dependency rules, module
isolation, DDD aggregate integrity, communication contracts, and naming conventions.
Every violation is classified as **BLOCKER / HIGH / MEDIUM** with a specific file:line and fix code.

---

## Allowed Dependency Map

```
Allowed dependency flow (→ = "may reference"):

MarketNest.Core                 ← references no module
MarketNest.{Module}.Domain      → Core only
MarketNest.{Module}.Application → Core + Module.Domain
MarketNest.{Module}.Infrastructure → Core + Module.Domain + Module.Application
MarketNest.Web                  → all (composition root — DI registration)

Modules (MUST NOT reference each other):
  Identity | Catalog | Cart | Orders | Payments | Reviews | Disputes | Notifications | Admin

Communication rules:
  Sync  → interface in Core (INotificationService, IPaymentGateway, ICatalogService ...)
  Async → IDomainEvent / IPublisher (MediatR Phase 1, MassTransit Phase 3+)
  DB    → each module only maps tables in its own schema (schema-per-module, ADR-004)
```

---

## Execution Flow

```
Phase 1: SCAN       → Collect solution structure, assemblies, project references
Phase 2: ANALYZE    → 6 rule groups, each with PowerShell scan + file review
Phase 3: REPORT     → Classify as BLOCKER / HIGH / MEDIUM with NetArchTest code snippets
Phase 4: FIX        → Propose specific refactoring (confirm before applying)
Phase 5: VERIFY     → Re-run checks + architecture tests
```

---

## Phase 1: SCAN — Collect Solution Structure

### 1.1 Enumerate project references

**PowerShell:**
```powershell
# List all project files
Get-ChildItem -Recurse -Filter *.csproj |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-Object FullName | Sort-Object FullName

# Read ProjectReference from each .csproj (actual dependency graph)
Get-ChildItem -Recurse -Filter *.csproj |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  ForEach-Object {
    Write-Host "=== $($_.Name) ==="
    Select-String -Path $_.FullName -Pattern 'ProjectReference' |
      ForEach-Object { $_.Line -replace '.*Include="([^"]+)".*', '$1' }
  }
```

### 1.2 Check for infrastructure packages in Domain projects

```powershell
# Flag any Domain project that imports EF Core / Redis / HTTP
Get-ChildItem -Recurse -Filter *.csproj |
  Where-Object { $_.Name -match 'Domain' -and $_.FullName -notmatch '\\(bin|obj)\\' } |
  ForEach-Object {
    $infra = Select-String -Path $_.FullName -Pattern 'EntityFramework|StackExchange\.Redis|System\.Net\.Http|Npgsql'
    if ($infra) {
        Write-Host "BLOCKER: Domain project references infrastructure: $($_.FullName)"
        $infra | ForEach-Object { Write-Host "  $($_.Line)" }
    }
  }
```

---

## Phase 2: ANALYZE — 6 Rule Groups

---

### Rule Group 1: Layer Dependency — Domain must not reference Infrastructure

**Rule**: `Domain` may only reference `Core` + BCL (`System.*`). Absolutely no EF Core, Redis, HTTP, MassTransit.

```powershell
# 1A. Find invalid using directives in Domain layer
Write-Host "=== Domain layer — infrastructure using directives ==="
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -match '\\Domain\\' -and $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern 'using Microsoft\.EntityFrameworkCore|using StackExchange\.Redis|using System\.Net\.Http|using MassTransit|using Npgsql|using MailKit|using Microsoft\.AspNetCore'

# 1B. Find Application layer referencing Infrastructure concrete types
Write-Host "=== Application layer — infrastructure using directives ==="
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -match '\\Application\\' -and $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern 'using Microsoft\.EntityFrameworkCore|using StackExchange\.Redis|using Npgsql|using MailKit'
# Note: Application may use MediatR — that is allowed
```

**BLOCKER example:**
```csharp
// ❌ BLOCKER: Domain/Entities/Order.cs
using Microsoft.EntityFrameworkCore; // EF Core in Domain!

public class Order : AggregateRoot
{
    [Column("order_status")] // EF annotation in Domain entity!
    public OrderStatus Status { get; set; }
}

// ✅ Entity config belongs in Infrastructure/Persistence/
// OrderConfiguration.cs in Infrastructure uses EF Fluent API
```

**Fix:**
```csharp
// Domain/Entities/Order.cs — pure domain code only
public class Order : AggregateRoot
{
    public OrderStatus Status { get; private set; }
    // No [Column], no [Key], no EF annotations
}

// Infrastructure/Persistence/Configurations/OrderConfiguration.cs
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", "orders");
        builder.Property(o => o.Status).HasConversion<string>();
    }
}
```

---

### Rule Group 2: Module Isolation — Modules must not reference each other

**Rule**: `MarketNest.Orders.*` must not use `MarketNest.Catalog.*` or any other module.
Only `MarketNest.Core` references are allowed across modules.

```powershell
$modules = @("Identity","Catalog","Cart","Orders","Payments","Reviews","Disputes","Notifications","Admin")

foreach ($module in $modules) {
    Write-Host "=== Checking $module for cross-module references ==="
    $path = "src/MarketNest.$module"
    if (Test-Path $path) {
        Get-ChildItem -Path $path -Recurse -Filter *.cs |
          Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
          Select-String -Pattern 'using MarketNest\.' |
          Where-Object {
            $_.Line -notmatch "using MarketNest\.Core\." -and
            $_.Line -notmatch "using MarketNest\.$module\." -and
            $_.Line -notmatch "using MarketNest\.Base\."
          } |
          Select-Object -ExpandProperty Line | Sort-Object | Get-Unique
    }
}
```

**BLOCKER example:**
```csharp
// ❌ BLOCKER: Orders/Application/Commands/PlaceOrderCommandHandler.cs
using MarketNest.Catalog.Domain.Entities;   // Orders knows about Catalog!
using MarketNest.Identity.Domain.Entities;  // Orders knows about Identity!

// ❌ Orders handler queries Catalog DbSet directly
public class PlaceOrderCommandHandler(CatalogDbContext db)
{
    public async Task Handle(...)
    {
        var product = await db.Products.FindAsync(productId); // BLOCKER!
    }
}
```

**Fix — use Core interface:**
```csharp
// ✅ Core/Contracts/ICatalogService.cs (neutral ground, no module affiliation)
public interface ICatalogService
{
    Task<ProductSnapshot?> GetProductSnapshotAsync(Guid variantId, CancellationToken ct);
}

// ✅ Catalog/Infrastructure/Services/CatalogService.cs — implementation
public class CatalogService(CatalogDbContext db) : ICatalogService
{
    public async Task<ProductSnapshot?> GetProductSnapshotAsync(Guid variantId, CancellationToken ct)
        => await db.ProductVariants
               .AsNoTracking()
               .Where(v => v.Id == variantId)
               .Select(v => new ProductSnapshot(v.Id, v.Product.Title, v.EffectivePrice()))
               .FirstOrDefaultAsync(ct);
}

// ✅ Orders/Application/Commands/PlaceOrderCommandHandler.cs — uses interface, not Catalog entity
public class PlaceOrderCommandHandler(ICatalogService catalog) { }
```

---

### Rule Group 3: DDD Aggregate Integrity

**Rule**: Aggregate root has private state, mutations via domain methods, events raised from
inside the aggregate, no anemic model.

```powershell
# 3A. Find public setters on Aggregate root entities (anemic model — also MN016)
Write-Host "=== Anemic Domain Model: public setters on aggregate entities ==="
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -match '\\Domain\\' -and $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern '\{ get; set; \}' |
  Select-Object Path, Line

# 3B. Find domain events raised from Application layer (should be inside aggregate)
Write-Host "=== Domain events raised from Application layer ==="
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -match '\\Application\\' -and $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern 'AddDomainEvent|new \w+Event\(' |
  Select-Object Path, Line

# 3C. Find Value Objects with public setters
Write-Host "=== Value Objects with mutable state ==="
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -match '\\ValueObjects\\' -and $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern '\{ get; set; \}' |
  Select-Object Path, Line
```

**BLOCKER examples:**
```csharp
// ❌ BLOCKER: Anemic domain model — MN016 also fires
public class Order : AggregateRoot
{
    public OrderStatus Status { get; set; }  // public setter!
    public decimal Total { get; set; }
}

// ❌ BLOCKER: Handler mutates state directly, bypassing invariants
public class ShipOrderCommandHandler
{
    public async Task Handle(ShipOrderCommand cmd, CancellationToken ct)
    {
        var order = await repo.GetByIdAsync(cmd.OrderId);
        order.Status = OrderStatus.Shipped;           // bypass invariant!
        order.ShippedAt = DateTimeOffset.UtcNow;
        // Domain event never raised → Notifications silently skipped!
    }
}

// ❌ BLOCKER: Domain event raised from handler, not from aggregate
public class PlaceOrderCommandHandler
{
    public async Task Handle(...)
    {
        await publisher.Publish(new OrderPlacedEvent(...)); // wrong — should be in aggregate
    }
}
```

**Correct pattern:**
```csharp
// ✅ Aggregate: private setter + domain method + event raised internally
public class Order : AggregateRoot
{
    public OrderStatus Status { get; private set; }
    public DateTimeOffset? ShippedAt { get; private set; }

    public Result<Unit, Error> MarkAsShipped(string trackingNumber)
    {
        if (Status != OrderStatus.Confirmed)
            return Errors.Order.InvalidTransition(Status, OrderStatus.Shipped);

        Status = OrderStatus.Shipped;
        ShippedAt = DateTimeOffset.UtcNow;

        // Event raised FROM the aggregate — dispatched by UoW after commit
        AddDomainEvent(new OrderShippedEvent(Id, BuyerId, trackingNumber));
        return Result.Success();
    }
}

// ✅ Handler: orchestrate only, never mutate state directly
// Note: DO NOT call uow.CommitAsync() — transaction filter owns this (ADR-027)
public partial class ShipOrderCommandHandler(IOrderRepository repo)
    : ICommandHandler<ShipOrderCommand, Unit>
{
    public async Task<Result<Unit, Error>> Handle(ShipOrderCommand cmd, CancellationToken ct)
    {
        var order = await repo.GetByIdAsync(cmd.OrderId, ct);
        if (order is null) return Errors.Order.NotFound(cmd.OrderId);

        var result = order.MarkAsShipped(cmd.TrackingNumber);
        if (result.IsFailure) return result;

        repo.Update(order);
        return Result.Success();
        // Transaction filter calls uow.CommitAsync() after this returns
    }
}
```

---

### Rule Group 4: Module Communication — Events and Interfaces Only

**Rule**: Module A notifying Module B → publish `IDomainEvent`. Module A querying data from
Module B → use an interface defined in `Core/Contracts/`.

```powershell
# 4A. Find direct concrete class injection across module boundaries
$modules = @("Identity","Catalog","Cart","Orders","Payments","Reviews","Disputes","Notifications")
foreach ($module in $modules) {
    Write-Host "=== $module — cross-module concrete service injection ==="
    $path = "src/MarketNest.$module/Application"
    if (Test-Path $path) {
        Get-ChildItem -Path $path -Recurse -Filter *.cs |
          Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
          Select-String -Pattern 'new MarketNest\.' |
          Where-Object { $_.Line -notmatch "MarketNest\.$module\." } |
          Select-Object Path, Line
    }
}

# 4B. Find HTTP calls between modules (forbidden inside the monolith)
Write-Host "=== Illegal HTTP calls between modules ==="
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -match '\\Application\\' -and $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern 'HttpClient|IHttpClientFactory|localhost:|127\.0\.0\.1' |
  Select-Object Path, Line
```

**HIGH example:**
```csharp
// ❌ HIGH: Orders injects concrete Notification service
public class PlaceOrderCommandHandler(NotificationService notificationService)  // wrong!

// ✅ Inject the interface from Core/Contracts/
public class PlaceOrderCommandHandler(INotificationService notifications)  // correct
```

**Fix — use integration event + IEventBus:**
```csharp
// ✅ Core defines shared integration event record
// Core/Common/Events/IntegrationEvents/OrderPlacedIntegrationEvent.cs
public record OrderPlacedIntegrationEvent(Guid OrderId, Guid BuyerId, decimal Total)
    : IIntegrationEvent;

// ✅ Orders publishes via IEventBus after domain event propagation
// Phase 1: InProcessEventBus (MediatR) — same process
// Phase 3: MassTransitEventBus (RabbitMQ) — single DI swap, no module code changes

// ✅ Notifications subscribes — no reference to Orders.Domain
public class OrderPlacedIntegrationEventHandler(INotificationDispatcher dispatcher)
    : IIntegrationEventHandler<OrderPlacedIntegrationEvent> { }
```

---

### Rule Group 5: Application Layer Contracts

**Rule**: Commands return `Result<T, Error>`. Queries are read-only. No manual validation in
handlers. Web layer sends via `ISender`, not Repository directly.

```powershell
# 5A. Command handlers returning void (must return Result)
Write-Host "=== Command handlers not returning Result ==="
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -match '\\Application\\' -and $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern 'public async Task Handle' |
  Where-Object { $_.Line -notmatch 'Result<' } |
  Select-Object Path, Line

# 5B. Query handlers calling SaveChanges (side effects in read path)
Write-Host "=== Query handlers with side effects ==="
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -match '\\Queries\\' -and $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern 'SaveChanges|CommitAsync|\.Add\(|\.Remove\(' |
  Select-Object Path, Line

# 5C. Web pages injecting Repository directly (bypassing CQRS)
Write-Host "=== Web layer bypassing MediatR ==="
Get-ChildItem -Path src/MarketNest.Web -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern 'IOrderRepository|ICatalogRepository|DbContext\b' |
  Select-Object Path, Line
```

**HIGH/MEDIUM examples + fixes:**
```csharp
// ❌ HIGH: Command handler returns void — caller cannot detect failure
public class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand>
{
    public async Task Handle(...) { }  // void — wrong!
}

// ✅ Always return Result
public class CancelOrderCommandHandler : ICommandHandler<CancelOrderCommand, Unit>
{
    public async Task<Result<Unit, Error>> Handle(...) { }
}

// ❌ MEDIUM: Razor Page injects Repository directly — bypasses CQRS
public class OrderDetailModel(IOrderRepository repo) : PageModel
{
    public async Task OnGet(Guid id) => Order = await repo.GetByIdAsync(id);
}

// ✅ Razor Page uses ISender from MediatR
public class OrderDetailModel(ISender sender) : PageModel
{
    public async Task OnGet(Guid id, CancellationToken ct)
    {
        var dto = await sender.Send(new GetOrderDetailQuery(id, User.GetUserId()), ct);
        if (dto is null) { RedirectToPage("/Error/404"); return; }
        Order = dto;
    }
}
```

---

### Rule Group 6: Naming and Structure Conventions

```powershell
# 6A. Domain events not ending in "Event" (MN015)
Write-Host "=== Domain events not ending in 'Event' ==="
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -match '\\Domain\\Events\\' -and $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern 'public record \w+' |
  Where-Object { $_.Line -notmatch 'Event\b' } |
  Select-Object Path, Line

# 6B. Commands not ending in "Command" (MN012)
Write-Host "=== Commands not ending in 'Command' ==="
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -match '\\Commands\\' -and $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern ': ICommand<|: ICommandHandler<' |
  Where-Object { $_.Line -notmatch 'Command\b' } |
  Select-Object Path, Line

# 6C. Namespace exceeds layer level (MN008)
Write-Host "=== Flat namespace violation ==="
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj|MarketNest\.Web)\\' } |
  Select-String -Pattern 'namespace MarketNest\.\w+\.\w+\.\w+' |
  Select-Object Path, Line

# 6D. Private fields not using _camelCase (MN001)
Write-Host "=== Private field naming violation ==="
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern 'private (readonly )?\w+ [a-zA-Z]' |
  Where-Object { $_.Line -notmatch 'private .* _' } |
  Select-Object Path, Line
```

---

## Phase 3: REPORT — Architecture Violation Report

```markdown
# Architecture Guard Report — MarketNest
**Date**: <date>
**Scope**: Clean Architecture, DDD, Module Isolation, Communication Contracts

---

## Summary

| Rule Group | Violations | Severity |
|---|---|---|
| Layer Dependency | X | BLOCKER |
| Module Isolation | X | BLOCKER |
| DDD Aggregate Integrity | X | BLOCKER / HIGH |
| Module Communication | X | HIGH |
| Application Contracts | X | HIGH / MEDIUM |
| Naming & Structure | X | MEDIUM |

---

## 🔴 BLOCKER — Must fix before merge

### [B-001] Domain references Infrastructure
- **File**: `src/MarketNest.Orders/Domain/Entities/Order.cs:3`
- **Violation**: `using Microsoft.EntityFrameworkCore;`
- **Rule**: Domain layer must not reference any infrastructure package (MN — layer dependency)
- **Fix**: Remove using, move EF config to `OrderConfiguration.cs` in Infrastructure

---

## 🟠 HIGH — Fix within current sprint

### [H-001] Cross-module entity access
- **File**: `src/MarketNest.Orders/Application/Commands/PlaceOrderCommandHandler.cs:45`
- **Violation**: `await db.Products.FindAsync(productId)` — Orders queries Catalog entity
- **Fix**: Create `ICatalogService` in `Core/Contracts/`, implement in `Catalog.Infrastructure`

---

## 🟡 MEDIUM — Backlog

...
```

---

## Phase 4: NetArchTest Code Snippets

Add these tests to `tests/MarketNest.ArchitectureTests/` to prevent regressions:

```csharp
// tests/MarketNest.ArchitectureTests/LayerDependencyTests.cs
public class LayerDependencyTests
{
    private static readonly Assembly CoreAssembly          = typeof(AggregateRoot).Assembly;
    private static readonly Assembly OrdersDomainAssembly  = typeof(Order).Assembly;
    private static readonly Assembly CatalogDomainAssembly = typeof(Product).Assembly;

    // ── Rule 1: Domain must not reference EF Core ─────────────────────────
    [Fact]
    public void Domain_ShouldNotDependOn_EntityFrameworkCore()
    {
        var result = Types.InAssembly(OrdersDomainAssembly)
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Domain layer must not reference EF Core. Violations: " +
                     string.Join(", ", result.FailingTypeNames ?? []));
    }

    // ── Rule 2: Module isolation ───────────────────────────────────────────
    [Fact]
    public void Orders_ShouldNotDependOn_CatalogDomain()
    {
        var result = Types.InAssembly(OrdersDomainAssembly)
            .Should()
            .NotHaveDependencyOn("MarketNest.Catalog")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Orders must not reference Catalog. Use ICatalogService interface.");
    }

    // ── Rule 3: Aggregate roots must not have public property setters ──────
    [Fact]
    public void AggregateRoots_ShouldNotHave_PublicPropertySetters()
    {
        var result = Types.InAssemblies([OrdersDomainAssembly, CatalogDomainAssembly])
            .That()
            .Inherit(typeof(AggregateRoot))
            .Should()
            .MeetCustomRule(new NoPublicSetterRule())
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Aggregate roots protect invariants via domain methods, not public setters.");
    }

    // ── Rule 4: Razor Pages must not inject Repositories directly ─────────
    [Fact]
    public void WebLayer_ShouldNotDependOn_Repositories()
    {
        var webAssembly = typeof(Program).Assembly;

        var result = Types.InAssembly(webAssembly)
            .That()
            .ResideInNamespace("MarketNest.Web.Pages")
            .Should()
            .NotHaveDependencyOn("IOrderRepository")
            .And()
            .NotHaveDependencyOn("ICatalogRepository")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: "Razor Pages must use ISender (MediatR), not repositories directly.");
    }

    // ── Rule 5: Domain event naming ────────────────────────────────────────
    [Fact]
    public void DomainEvents_ShouldEndWith_Event()
    {
        var result = Types.InAssemblies([OrdersDomainAssembly, CatalogDomainAssembly])
            .That()
            .ImplementInterface(typeof(IDomainEvent))
            .Should()
            .HaveNameEndingWith("Event")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}

// Custom NetArchTest rule for public setters
public class NoPublicSetterRule : ICustomRule
{
    public bool MeetsRule(TypeDefinition type)
        => !type.Properties.Any(p => p.SetMethod is { IsPublic: true });
}
```

---

## Phase 5: VERIFY — Post-fix Verification

```powershell
# Build to catch compiler errors
dotnet build MarketNest.slnx --no-incremental 2>&1 | Where-Object { $_ -match 'error|warning' }

# Run Architecture Tests (fast, no Docker needed)
dotnet test tests/MarketNest.ArchitectureTests -v normal

# Run Unit Tests (verify domain logic not broken)
dotnet test tests/MarketNest.UnitTests -v normal

# Re-run cross-module check — expect 0 results
Write-Host "=== Re-checking cross-module references ==="
$modules = @("Identity","Catalog","Cart","Orders","Payments","Reviews","Disputes","Notifications","Admin")
foreach ($module in $modules) {
    Get-ChildItem -Path "src/MarketNest.$module" -Recurse -Filter *.cs -ErrorAction SilentlyContinue |
      Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
      Select-String -Pattern 'using MarketNest\.' |
      Where-Object {
        $_.Line -notmatch "using MarketNest\.Core\." -and
        $_.Line -notmatch "using MarketNest\.$module\." -and
        $_.Line -notmatch "using MarketNest\.Base\."
      } |
      Select-Object Path, Line
}

# Re-check public setters on aggregates — expect 0 results
Write-Host "=== Re-checking public setters on aggregates ==="
Get-ChildItem -Recurse -Filter *.cs |
  Where-Object { $_.FullName -match '\\Domain\\Entities\\' -and $_.FullName -notmatch '\\(bin|obj)\\' } |
  Select-String -Pattern '\{ get; set; \}' |
  Select-Object Path, Line
```

---

## Quick Reference — Architecture Rules

| Rule | Allowed | NOT Allowed |
|---|---|---|
| Domain references | `Core`, BCL (`System.*`) | EF Core, Redis, HTTP, MassTransit |
| Application references | `Core`, `Domain`, MediatR | EF Core concrete, Redis concrete |
| Infrastructure references | All within same module | Other modules |
| Module → Module | Via `Core` interface | Direct namespace reference |
| DB cross-module access | Never | `db.Products` from Orders handler |
| State mutation | `order.MarkAsShipped()` | `order.Status = OrderStatus.Shipped` |
| Event raise location | Inside aggregate method | Application handler |
| Command return type | `Result<T, Error>` | `void`, `Task` |
| Web layer | `ISender.Send()` | Repository, DbContext |
| Async communication | `IDomainEvent` + `IPublisher` | Direct cross-module method call |
| Transaction commit (HTTP) | Transaction filter does it — handler must NOT commit | Handler calling `uow.CommitAsync()` |
| Transaction commit (background jobs) | Handler MUST call `uow.CommitAsync(ct)` explicitly | Relying on filter (no HTTP context) |
