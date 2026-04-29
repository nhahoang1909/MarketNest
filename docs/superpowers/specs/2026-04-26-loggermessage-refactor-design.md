# LoggerMessage Refactor — Design Spec

**Date**: 2026-04-26  
**Branch**: p1-main-nhahoang  
**Status**: Approved — ready for implementation plan  
**Related ADR**: ADR-014

---

## 1. Problem Statement

The current codebase uses `IAppLogger<T>` with dynamic message templates (`.Info("template {X}", x)`),
which triggers two compiler/analyzer warnings that are suppressed with `#pragma`:

| Warning | Description |
|---------|-------------|
| CA1848 | Use `LoggerMessage` delegates for logging — avoid using extension methods |
| CA2254 | Message template must be a static expression — variable templates lose structured logging benefits |

Beyond warnings, the current approach has runtime costs:
- Message template parsed on every call even when log level is disabled
- Value types (Guid, int, decimal) boxed to `object?[]` on every call
- No EventId → can't filter precisely in Seq

---

## 2. Goals

- Migrate all production logging to `[LoggerMessage]` source-generated delegates
- Extend `IAppLogger<T>` to implement `ILogger` (prerequisite for delegate compatibility)
- Add new logging to pages and handlers that currently have zero observability
- Eliminate `#pragma warning disable CA1848, CA2254` from `AppLogger.cs`
- Assign stable EventIds per module for Seq filtering

---

## 3. Out of Scope

- Test code and dev-only seeders
- Domain modules that are currently empty placeholders (Identity, Catalog, Cart, Orders) — these will get logging when their handlers are implemented
- Modifying the Seq/Serilog configuration

---

## 4. Key Design Decisions

### 4.1 IAppLogger\<T\> Compatibility (Approach A)

`IAppLogger<T>` currently does NOT implement `ILogger`. `[LoggerMessage]` delegates require
`ILogger` as their first parameter. Fix: extend the interface.

**IAppLogger\<T\>** — add `: ILogger`:
```csharp
public interface IAppLogger<T> : ILogger
{
    // existing methods unchanged
}
```

**AppLogger\<T\>** — implement 3 explicit ILogger members using `inner.Log()` (core method,
not extension methods, so CA1848 does not fire):
```csharp
void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state,
    Exception? exception, Func<TState, Exception?, string> formatter)
    => inner.Log(logLevel, eventId, state, exception, formatter);

bool ILogger.IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

IDisposable? ILogger.BeginScope<TState>(TState state) => inner.BeginScope(state);
```

After all call sites are migrated, the `.Info()` / `.Warn()` / `.Error()` methods on
`IAppLogger<T>` become dead code and are removed in the Cleanup step. This eliminates
the remaining CA suppressions.

**End state of IAppLogger\<T\>:**
```csharp
// Marker interface for DI — just extends ILogger
public interface IAppLogger<T> : ILogger { }

// AppLogger<T> — only 3 explicit ILogger methods, zero pragma needed
public sealed class AppLogger<T>(ILogger<T> inner) : IAppLogger<T>
{
    void ILogger.Log<TState>(...) => inner.Log(...);
    bool ILogger.IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);
    IDisposable? ILogger.BeginScope<TState>(TState state) => inner.BeginScope(state);
}
```

### 4.2 Parallel Execution (Approach 2)

After the prerequisite step, four independent agent groups work in parallel:

```
[Step 0 - Prerequisite]
  Extend IAppLogger<T> + AppLogger<T>

[Step 1 - Parallel agents]
  Agent 1: Infra + Middleware   (6 files, EventId 10000–19999)
  Agent 2: Web Pages existing   (21 files, EventId per module)
  Agent 3: Auditing + Jobs      (5 files, EventId 110000–129999)
  Agent 4: New logging — Pages  (19 files, EventId per module)

[Step 2 - Cleanup]
  Strip IAppLogger<T> dead methods, remove pragma

[Step 3 - Verify]
  dotnet build (TreatWarningsAsErrors=true) + dotnet test
```

---

## 5. EventId Allocation

Each module owns a block of 10,000 EventIds. Sub-allocation within each block:

| Range offset | Layer |
|---|---|
| X0000–X1999 | Infrastructure / Persistence |
| X2000–X5999 | Application layer (Command/Query handlers) |
| X6000–X7999 | Web Pages (PageModel handlers) |
| X8000–X9999 | Reserved |

| Module | EventId Range |
|--------|--------------|
| Infrastructure / Middleware | 10000–19999 |
| Identity | 20000–29999 |
| Catalog | 30000–39999 |
| Cart | 40000–49999 |
| Orders | 50000–59999 |
| Payments | 60000–69999 |
| Reviews | 70000–79999 |
| Disputes | 80000–89999 |
| Notifications | 90000–99999 |
| Admin | 100000–109999 |
| Auditing | 110000–119999 |
| Background Jobs | 120000–129999 |
| Web / Global Pages | 130000–139999 |
| Promotions | 140000–149999 |
| *(Reserved — future modules)* | 150000+ |

Web Pages for a specific module use that module's range (e.g., Checkout page → Orders range 56000–57999).
Global pages (Error, Index, NotFound) use the Web/Global range 130000–139999.

---

## 6. Naming Convention

Delegate method names follow `{LogLevel}{Subject}{Event}`:

| Example | Meaning |
|---------|---------|
| `InfoStart` | Entry point of a handler |
| `InfoOrderPlaced` | Happy path success |
| `WarnCartItemOutOfStock` | Business rejection |
| `ErrorPaymentGatewayTimeout` | Unexpected failure |
| `DebugCacheHit` | Debug-level diagnostic |

---

## 7. Standard Pattern

Every class that logs must:
1. Add `partial` keyword to the class declaration
2. Create `private static partial class Log` at the bottom of the file
3. Replace every `.Info()` / `.Warn()` / `.Error()` call with a `Log.Xxx(_logger, ...)` delegate

```csharp
public partial class PlaceOrderCommandHandler(...) : ICommandHandler<...>
{
    public async Task<Result<OrderDto, Error>> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        Log.InfoStart(_logger, cmd.BuyerId, cmd.CartId);
        try
        {
            // business logic...
            Log.InfoOrderPlaced(_logger, order.Id, order.Total.Amount);
            return Result.Ok(dto);
        }
        catch (Exception ex)
        {
            Log.ErrorUnexpected(_logger, cmd.BuyerId, ex);
            throw;
        }
    }

    private static partial class Log
    {
        [LoggerMessage(5200, LogLevel.Information,
            "PlaceOrder Start - BuyerId={BuyerId} CartId={CartId}")]
        public static partial void InfoStart(ILogger logger, Guid buyerId, Guid cartId);

        [LoggerMessage(5201, LogLevel.Information,
            "PlaceOrder Success - OrderId={OrderId} Total={Total:C}")]
        public static partial void InfoOrderPlaced(ILogger logger, Guid orderId, decimal total);

        [LoggerMessage(5202, LogLevel.Error,
            "PlaceOrder Unexpected Error - BuyerId={BuyerId}")]
        public static partial void ErrorUnexpected(ILogger logger, Guid buyerId, Exception ex);
    }
}
```

Key rules:
- `Exception` is always the last parameter — never written in the message template
- No PII: log IDs only, not email / name / address
- No anonymous objects: `new { OrderId, BuyerId }` → separate typed params
- Template must be a const string literal (no interpolation)
- **All EventIds must reference `LogEventId` enum** — no raw integer literals:
  `[LoggerMessage((int)LogEventId.PlaceOrderStart, LogLevel.Information, "...")]`
- EventId enum lives in `MarketNest.Base.Infrastructure/Logging/LogEventId.cs`

---

## 8. New Logging for Pages with Zero Coverage (Agent 4)

Pages that currently have 0 log calls get a minimum of 3 delegates per handler:

```
InfoStart       — operation started (includes CorrelationId)
InfoSuccess     — result + ElapsedMs
WarnXxx         — business rejection (NotFound, Unauthorized) if applicable
ErrorUnexpected — catch-all Exception
```

---

## 9. File Inventory

### Agent 1 — Infrastructure + Middleware (EventId 10000–19999)

| File | Current calls |
|------|--------------|
| `Web/Infrastructure/DatabaseInitializer.cs` | 16 |
| `Web/Infrastructure/DatabaseTracker.cs` | 5 |
| `Web/Infrastructure/InProcessEventBus.cs` | 3 |
| `Web/Infrastructure/MassTransitEventBus.cs` | 2 |
| `Web/Infrastructure/ApiContractGenerator.cs` | 2 |
| `Web/Infrastructure/RouteWhitelistMiddleware.cs` | 1 |

### Agent 2 — Web Pages with existing logging (EventId per module)

| File | Module | EventId sub-range |
|------|--------|------------------|
| `Pages/Auth/Login.cshtml.cs` | Identity | 26000–26199 |
| `Pages/Auth/Register.cshtml.cs` | Identity | 26000–26199 |
| `Pages/Auth/ForgotPassword.cshtml.cs` | Identity | 26000–26199 |
| `Pages/Account/Orders/Review.cshtml.cs` | Identity | 26200–26599 |
| `Pages/Account/Settings/Index.cshtml.cs` | Identity | 26200–26599 |
| `Pages/Account/Orders/Detail.cshtml.cs` | Identity | 26200–26599 |
| `Pages/Account/Orders/Index.cshtml.cs` | Identity | 26200–26599 |
| `Pages/Account/Disputes/Detail.cshtml.cs` | Identity | 26200–26599 |
| `Pages/Account/Disputes/Index.cshtml.cs` | Identity | 26200–26599 |
| `Pages/Admin/Config/Commission.cshtml.cs` | Admin | 106000–106999 |
| `Pages/Admin/Dashboard/Index.cshtml.cs` | Admin | 106000–106999 |
| `Pages/Admin/Config/Index.cshtml.cs` | Admin | 106000–106999 |
| `Pages/Admin/Disputes/Index.cshtml.cs` | Admin | 106000–106999 |
| `Pages/Admin/Notifications/Index.cshtml.cs` | Admin | 106000–106999 |
| `Pages/Admin/Products/Index.cshtml.cs` | Admin | 106000–106999 |
| `Pages/Admin/Storefronts/Index.cshtml.cs` | Admin | 106000–106999 |
| `Pages/Admin/Users/Index.cshtml.cs` | Admin | 106000–106999 |
| `Pages/Cart/Index.cshtml.cs` | Cart | 46000–46499 |
| `Pages/Checkout/Index.cshtml.cs` | Orders | 56000–56499 |
| `Pages/Error.cshtml.cs` | Global | 130000–130099 |
| `Pages/Index.cshtml.cs` | Global | 130000–130099 |

### Agent 3 — Auditing + Background Jobs

| File | EventId range |
|------|--------------|
| `Auditing/Infrastructure/AuditService.cs` | 110000–110099 |
| `Auditing/Infrastructure/AuditBehavior.cs` | 110100–110199 |
| `Auditing/Infrastructure/AuditableInterceptor.cs` | 110200–110299 |
| `Web/Hosting/JobRunnerHostedService.cs` | 120000–120099 |
| `Admin/Application/Timer/TestTimerJob.cs` | 121000–121099 |

### Agent 4 — New logging: Pages with zero coverage

| File | Module | EventId sub-range |
|------|--------|------------------|
| `Pages/Seller/Products/Create.cshtml.cs` | Catalog | 36000–36499 |
| `Pages/Seller/Products/Edit.cshtml.cs` | Catalog | 36000–36499 |
| `Pages/Seller/Products/Index.cshtml.cs` | Catalog | 36000–36499 |
| `Pages/Seller/Products/Variants.cshtml.cs` | Catalog | 36000–36499 |
| `Pages/Shop/Index.cshtml.cs` | Catalog | 37000–37499 |
| `Pages/Shop/Products/Detail.cshtml.cs` | Catalog | 37000–37499 |
| `Pages/Search/Index.cshtml.cs` | Catalog | 37000–37499 |
| `Pages/Seller/Orders/Index.cshtml.cs` | Orders | 57000–57499 |
| `Pages/Seller/Orders/Detail.cshtml.cs` | Orders | 57000–57499 |
| `Pages/Orders/Confirmation.cshtml.cs` | Orders | 57000–57499 |
| `Pages/Seller/Dashboard/Index.cshtml.cs` | Admin | 107000–107499 |
| `Pages/Seller/Storefront/Index.cshtml.cs` | Admin | 107000–107499 |
| `Pages/Seller/Reviews/Index.cshtml.cs` | Admin | 107000–107499 |
| `Pages/Seller/Disputes/Index.cshtml.cs` | Admin | 107000–107499 |
| `Pages/Seller/Payouts/Index.cshtml.cs` | Admin | 107000–107499 |
| `Pages/Admin/Handlers/CreateTestHandler.cs` | Admin | 102000–102499 |
| `Pages/Admin/Handlers/UpdateTestHandler.cs` | Admin | 102000–102499 |
| `Pages/Admin/Handlers/GetTestByIdHandler.cs` | Admin | 102000–102499 |
| `Pages/Admin/Handlers/GetTestsPagedHandler.cs` | Admin | 102000–102499 |
| `Pages/NotFound.cshtml.cs` | Global | 13100–13199 |

---

## 10. Cleanup Step (after all agents)

1. Remove `.Info()` / `.Debug()` / `.Warn()` / `.Error()` / `.Critical()` / `.Trace()` methods from `IAppLogger<T>`
2. Remove corresponding implementations from `AppLogger<T>`
3. Remove `#pragma warning disable CA1848, CA2254`
4. Run `dotnet build` — verify zero warnings
5. Run `dotnet test` — verify all tests pass
