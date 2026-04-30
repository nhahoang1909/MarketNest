# MarketNest Roslyn Analyzers Reference

> Design spec: `docs/superpowers/specs/2026-04-27-roslyn-analyzers-design.md`
> Status: **Updated** (2026-04-30) — all 33 rules + 6 code fixes implemented, wired to all src/ projects

## Overview

`src/MarketNest.Analyzers/` is a `netstandard2.0` Roslyn analyzer project that enforces the coding rules in `docs/code-rules.md` at **build time**. Violations appear as IDE squiggly lines and fail the CI build. Six rules ship with Quick Action code fixes.

The analyzer is wired to every project under `src/` via `src/Directory.Build.targets`:

```xml
<Project>
  <ItemGroup Condition="'$(MSBuildProjectName)' != 'MarketNest.Analyzers'">
    <ProjectReference Include="$(MSBuildThisFileDirectory)MarketNest.Analyzers/MarketNest.Analyzers.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
```

---

## Diagnostic IDs

| ID | Rule | Category | Severity | Code Fix |
|----|------|----------|----------|----------|
| MN001 | Private field must use `_camelCase` | Naming | Error | ✅ |
| MN002 | Banned class suffix (`Manager`, `Helper`, `Utils`) | Naming | Warning | ❌ |
| MN003 | `async void` method | Async | Error | ✅ |
| MN004 | Blocking on async (`.GetAwaiter().GetResult()`, `.Result`, `.Wait()`) | Async | Error | ❌ |
| MN005 | Direct `ILogger`/`ILogger<T>` call (use `[LoggerMessage]`) | Logging | Error | ❌ |
| MN006 | Logging class must be `partial` | Logging | Error | ✅ |
| MN007 | Inject `IAppLogger<T>` instead of `ILogger<T>` | Logging | Error | ✅ |
| MN008 | Namespace exceeds `MarketNest.<Module>.<Layer>` | Architecture | Error | ❌ |
| MN009 | Use `DateTimeOffset` instead of `DateTime` | Architecture | Warning | ❌ |
| MN010 | Service-locator anti-pattern inside handlers | Architecture | Error | ❌ |
| MN011 | Public async API missing `CancellationToken` | Async | Warning | ❌ |
| MN012 | `ICommand<>` class name must end with `Command` | Naming | Warning | ❌ |
| MN013 | `IQuery<>` class name must end with `Query` | Naming | Warning | ❌ |
| MN014 | Handler class name must end with `Handler` | Naming | Warning | ❌ |
| MN015 | Domain event record name must end with `Event` | Naming | Warning | ❌ |
| MN016 | Entity/Aggregate property must not have public setter | Architecture | Error | ❌ |
| MN017 | Unnecessary `Task.FromResult(x)` (use `ValueTask` or return directly) | Async | Warning | ✅ |
| MN018 | Insecure hash algorithm (MD5, SHA256 — use SHA512+) | Security | Error | ✅ |
| MN019 | Handler must not return entity type directly — return a DTO | Architecture | Warning | ❌ |
| MN020 | QueryHandler query is missing a `.Select()` projection | Architecture | Warning | ❌ |
| MN021 | Banned `Service` suffix on concrete class (unless implements `I*Service`) | Naming | Warning | ❌ |
| MN022 | Banned `Impl` class suffix | Naming | Warning | ❌ |
| MN023 | Fire-and-forget async call (unawaited `Task`-returning method) | Async | Error | ❌ |
| MN024 | Command handler calls `SaveChangesAsync`/`CommitAsync` directly (ADR-027) | Architecture | Error | ❌ |
| MN025 | Handler calls `BeginTransactionAsync` (ADR-027) | Architecture | Error | ❌ |
| MN026 | Domain layer references infrastructure namespace (EF Core, Redis, etc.) | Architecture | Error | ❌ |
| MN027 | Repository interface returns `IQueryable<T>` (leaks EF into domain) | Architecture | Warning | ❌ |
| MN028 | Entity/Aggregate property uses `init` accessor (bypasses domain guards) | Architecture | Error | ❌ |
| MN029 | Query handler missing `AsNoTracking()` call | Architecture | Warning | ❌ |
| MN030 | Handler/PageModel injects concrete class instead of interface | Architecture | Warning | ❌ |
| MN031 | Query handler calls `SaveChanges`/`CommitAsync` (queries must be read-only) | Architecture | Error | ❌ |
| MN032 | `.Include()` chain exceeds 3 levels deep | Architecture | Warning | ❌ |
| MN033 | Cache usage (`ICacheService`, `CacheKeys`) in Domain layer | Architecture | Error | ❌ |

> `TreatWarningsAsErrors=true` is set in `Directory.Build.props`, so all warnings also fail the build.

---

## Project Structure

```
src/MarketNest.Analyzers/
  Analyzers/
    Naming/            PrivateFieldNamingAnalyzer, BannedClassSuffixAnalyzer, CommandQueryNamingAnalyzer,
                       BannedServiceSuffixAnalyzer, BannedImplSuffixAnalyzer
    AsyncRules/        AsyncVoidAnalyzer, BlockingAsyncAnalyzer, TaskFromResultAnalyzer, CancellationTokenAnalyzer,
                       FireAndForgetAnalyzer
    Logging/           DirectLoggerCallAnalyzer, LoggingClassPartialAnalyzer, AppLoggerInjectionAnalyzer
    Architecture/      FlatNamespaceAnalyzer, DateTimeUsageAnalyzer, ServiceLocatorAnalyzer, EntityPublicSetterAnalyzer, InsecureHashAnalyzer,
                       HandlerEntityReturnAnalyzer, HandlerQueryProjectionAnalyzer, HandlerSaveChangesAnalyzer,
                       HandlerTransactionAnalyzer, DomainInfrastructureReferenceAnalyzer, RepositoryIQueryableAnalyzer,
                       EntityInitAccessorAnalyzer, QueryHandlerNoTrackingAnalyzer, ConcreteInjectionAnalyzer,
                       QueryHandlerSaveChangesAnalyzer, DeepIncludeChainAnalyzer, DomainCacheUsageAnalyzer
  CodeFixes/           PrivateFieldNamingCodeFix, AsyncVoidCodeFix, LoggingClassPartialCodeFix,
                       AppLoggerInjectionCodeFix, TaskFromResultCodeFix, InsecureHashCodeFix
  DiagnosticIds.cs     All 33 ID constants

tests/MarketNest.Analyzers.Tests/
  Naming/ AsyncRules/ Logging/ Architecture/   — one test class per analyzer (130+ tests total)
```

---

## Suppression Patterns

Suppress individual violations with `#pragma`:

```csharp
#pragma warning disable MN009 // DateTimeOffset not applicable here — infrastructure model
public DateTime CreatedAt { get; set; }
#pragma warning restore MN009
```

### Known intentional suppressions

| Location | Rule | Reason |
|----------|------|--------|
| `AppLogger.cs` | MN007 | `AppLogger<T>` IS the `IAppLogger` implementation — must accept `ILogger<T>` |
| `NpgsqlJobExecutionStore.cs` | MN004 | Constructor cannot be `async` — one-time idempotent DDL bootstrap |
| `MarketNest.Web.csproj` | MN008 | Razor Pages use folder-matched namespaces; `@model` directives and `IndexModel` class-name collisions make flat namespaces impossible |

---

## Fixing Common Violations

### MN001 — Field naming
```csharp
// Bad
private int count;
// Good (Quick Action available)
private int _count;
```

### MN003 — async void
```csharp
// Bad
public async void OnClick() { ... }
// Good (Quick Action available)
public async Task OnClick() { ... }
```

### MN005 / MN006 / MN007 — Logging
```csharp
// Bad
public class MyService(ILogger<MyService> logger) { ... }
// Good
public partial class MyService(IAppLogger<MyService> logger)
{
    private static partial class Log
    {
        [LoggerMessage(1001, LogLevel.Information, "Doing thing: {Id}")]
        public static partial void InfoDoingThing(ILogger logger, Guid id);
    }
}
```

### MN008 — Flat namespace
```csharp
// Bad (in src/MarketNest.Identity/Application/Commands/)
namespace MarketNest.Identity.Application.Commands;
// Good
namespace MarketNest.Identity.Application;
```

### MN009 — DateTimeOffset
```csharp
// Bad
public DateTime CreatedAt { get; private set; }
// Good
public DateTimeOffset CreatedAt { get; private set; }
```

### MN016 — Entity setter
```csharp
// Bad
public string Name { get; set; }  // in an Entity<T> subclass
// Good
public string Name { get; private set; }
// Mutate via domain method:
public void Rename(string newName) { Name = newName; }
```

### MN018 — Insecure hash algorithm
```csharp
// Bad
using System.Security.Cryptography;
var md5Hash = MD5.HashData(data);
var sha256Hash = SHA256.HashData(data);

// Good (Quick Action available)
using System.Security.Cryptography;
var sha512Hash = SHA512.HashData(data);
```

### MN019 — Handler must not return entity directly

QueryHandlers and CommandHandlers must never return `Entity<T>` or `AggregateRoot` subtypes.
Return a DTO or result record instead. The rule also fires when the entity is wrapped inside
`Result<T, Error>`, `IEnumerable<T>`, `IReadOnlyList<T>`, etc.

```csharp
// ❌ Violates MN019 — leaks domain model through the handler boundary
class GetOrderByIdQueryHandler : IQueryHandler<GetOrderByIdQuery, Order> { ... }
class ListOrdersQueryHandler : IQueryHandler<ListOrdersQuery, IReadOnlyList<Order>> { ... }

// ✅ Fixed — project to a DTO
class GetOrderByIdQueryHandler : IQueryHandler<GetOrderByIdQuery, OrderDetailDto> { ... }
class ListOrdersQueryHandler : IQueryHandler<ListOrdersQuery, IReadOnlyList<OrderSummaryDto>> { ... }
```

**Suppression**: Use `#pragma warning disable MN019` only when intentionally returning a value
object (never an Entity). Add a comment explaining why.

### MN020 — QueryHandler query missing Select projection

QueryHandlers (classes implementing `IQueryHandler<,>`) and BaseQuery subclasses must include
a `.Select()` or `.SelectMany()` before calling any terminal operator (`ToListAsync`,
`FirstOrDefaultAsync`, etc.). This prevents loading all entity columns from the database.

CommandHandlers are **intentionally excluded** — they often need the full aggregate state
to enforce invariants.

```csharp
// ❌ Violates MN020 — loads every column of the entity
var orders = await _db.Orders
    .Where(o => o.SellerId == sellerId)
    .ToListAsync(ct);   // no Select!

// ✅ Fixed — project only what the use-case needs
var orders = await _db.Orders
    .Where(o => o.SellerId == sellerId)
    .Select(o => new OrderSummaryDto
    {
        Id = o.Id,
        TotalAmount = o.TotalAmount,
        Status = o.Status,
    })
    .ToListAsync(ct);

// ✅ CommandHandler exemption — suppress when loading aggregate for mutation
#pragma warning disable MN020 // Full aggregate needed to enforce payment invariants
var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
#pragma warning restore MN020
```

---

### MN021 — Banned 'Service' suffix on concrete class

Concrete classes must not use the `Service` suffix unless they implement a matching `I{Name}Service` interface. The suffix is too generic.

```csharp
// ❌ Violates MN021
class OrderService { }

// ✅ Fixed — implements matching interface
class NotificationService : INotificationService { }
// ✅ Fixed — use a descriptive name
class OrderProcessor { }
```

### MN022 — Banned 'Impl' suffix

Class names ending in `Impl` are banned — the suffix adds no value.

```csharp
// ❌ Violates MN022
class OrderRepositoryImpl : IOrderRepository { }

// ✅ Fixed
class SqlOrderRepository : IOrderRepository { }
```

### MN023 — Fire-and-forget async call

Calling a `Task`/`ValueTask`-returning method without `await` silently swallows exceptions.

```csharp
// ❌ Violates MN023
DoWorkAsync();

// ✅ Fixed
await DoWorkAsync();
```

### MN024 — Handler calls SaveChangesAsync/CommitAsync

Command handlers must never call `SaveChangesAsync()` or `CommitAsync()` — the transaction filter handles commit (ADR-027).

```csharp
// ❌ Violates MN024
class PlaceOrderHandler : ICommandHandler<PlaceOrderCommand, Guid> {
    public async Task<Result<Guid, Error>> Handle(...) {
        _repo.Add(order);
        await _uow.CommitAsync(ct); // ← banned
        return order.Id;
    }
}

// ✅ Fixed — just return, transaction filter commits
class PlaceOrderHandler : ICommandHandler<PlaceOrderCommand, Guid> {
    public async Task<Result<Guid, Error>> Handle(...) {
        _repo.Add(order);
        return order.Id;
    }
}
```

### MN025 — Handler calls BeginTransactionAsync

Handlers must not manage transactions — only background jobs may call `BeginTransactionAsync`.

### MN026 — Domain references infrastructure namespace

Files in `*.Domain` namespaces must not use `Microsoft.EntityFrameworkCore`, `StackExchange.Redis`, `System.Net.Http`, `Microsoft.AspNetCore`, `Npgsql`, `MassTransit`, or `RabbitMQ`.

```csharp
// ❌ Violates MN026 (in Domain layer)
using Microsoft.EntityFrameworkCore;
namespace MarketNest.Orders.Domain;

// ✅ Fixed — move EF usage to Infrastructure layer
```

### MN027 — Repository returns IQueryable

Repository interfaces must not return `IQueryable<T>` — this leaks EF Core into the domain.

```csharp
// ❌ Violates MN027
interface IOrderRepository {
    IQueryable<Order> GetAll();
}

// ✅ Fixed
interface IOrderRepository {
    Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken ct);
}
```

### MN028 — Entity uses init accessor

Entity/AggregateRoot properties must not use `{ get; init; }` — it bypasses domain method guards.

```csharp
// ❌ Violates MN028
class Order : Entity<Guid> {
    public string Status { get; init; }  // ← bypasses guards
}

// ✅ Fixed
class Order : Entity<Guid> {
    public string Status { get; private set; }
    public void UpdateStatus(string newStatus) { Status = newStatus; }
}
```

### MN029 — Query handler missing AsNoTracking

Query handlers should always include `AsNoTracking()` since they are read-only operations.

```csharp
// ❌ Violates MN029
var orders = await _db.Orders.ToListAsync(ct);

// ✅ Fixed
var orders = await _db.Orders.AsNoTracking().ToListAsync(ct);
```

### MN030 — Inject concrete class instead of interface

Handler/PageModel constructor parameters should be interfaces, not concrete classes.

```csharp
// ❌ Violates MN030
class MyHandler(OrderRepository repo) : ICommandHandler<...> { }

// ✅ Fixed
class MyHandler(IOrderRepository repo) : ICommandHandler<...> { }
```

### MN031 — Query handler calls SaveChanges

Query handlers must never call `SaveChanges`/`SaveChangesAsync`/`CommitAsync` — queries are strictly read-only.

### MN032 — Deep Include chain

`.Include()` chains must not exceed 3 levels deep (1 Include + 2 ThenInclude). Use `.Select()` projections instead.

```csharp
// ❌ Violates MN032 — 4 levels deep
_db.Orders
    .Include(o => o.Items)
    .ThenInclude(i => i.Product)
    .ThenInclude(p => p.Category)
    .ThenInclude(c => c.Parent);  // ← 4th level

// ✅ Fixed — use projection
_db.Orders.Select(o => new OrderDetailDto { ... }).ToListAsync(ct);
```

### MN033 — Cache usage in Domain layer

`ICacheService`, `CacheKeys`, `IDistributedCache`, and `IMemoryCache` must not be used in `*.Domain` namespaces. Caching is an infrastructure concern.

---

## Testing

Run analyzer tests in isolation:
```bash
dotnet test tests/MarketNest.Analyzers.Tests/
```

All 130+ tests should pass. Each test class uses the `Microsoft.CodeAnalysis.CSharp.Testing` framework with inline source markup:
```csharp
await Verify<MyAnalyzer>("""
    namespace MarketNest.Identity.Application.{|MN008:Commands|}; // <- diagnostic span
    """);
```

---

## Adding a New Rule

1. Add a `const string MNxxx = "MNxxx";` to `DiagnosticIds.cs`
2. Create `Analyzers/<Category>/MyAnalyzer.cs` implementing `DiagnosticAnalyzer`
3. Optionally create `CodeFixes/MyCodeFix.cs` implementing `CodeFixProvider`
4. Create `tests/MarketNest.Analyzers.Tests/<Category>/MyAnalyzerTests.cs`
5. Run `dotnet test tests/MarketNest.Analyzers.Tests/` — all tests must pass
6. Run `dotnet build MarketNest.slnx` — zero errors/warnings
