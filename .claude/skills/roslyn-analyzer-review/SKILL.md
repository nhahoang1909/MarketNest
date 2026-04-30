---
name: roslyn-analyzer-review
description: >
  Review and validate MarketNest's 18 custom Roslyn analyzer rules (MN001–MN018).
  Use this skill when the user wants to: understand a build error MN001–MN018, add a new analyzer
  rule, write analyzer unit tests, suppress a rule intentionally, debug why an analyzer fires,
  audit the project for analyzer violations, review a new DiagnosticAnalyzer implementation,
  or check analyzer test coverage. Activate when the user says anything like "MN0xx error",
  "analyzer violation", "build error", "add new rule", "write analyzer test", "suppress MN",
  or pastes a C# file with a build error referencing MN0xx.
compatibility:
  tools: [bash, read_file, write_file, list_files, grep_search, run_in_terminal]
  agents: [claude-code, gemini-cli, cursor, continue, aider, copilot]
  stack: [.NET 10, Roslyn SDK, Microsoft.CodeAnalysis.CSharp.Testing, xUnit]
---

# Roslyn Analyzer Review Skill — MarketNest

This skill covers the 18 custom Roslyn diagnostic rules (`MN001`–`MN018`) defined in
`src/MarketNest.Analyzers/`. These rules enforce coding standards from `docs/code-rules.md`
at **build time** — violations produce IDE squiggly lines and fail CI.

> Rule authority: `docs/analyzers.md` and `docs/code-rules.md`.
> Run `dotnet test tests/MarketNest.Analyzers.Tests/` to validate after any change.

---

## Quick Reference — All 18 Rules

| ID | Category | Rule | Severity | Code Fix |
|----|----------|------|----------|----------|
| MN001 | Naming | Private field must use `_camelCase` | Error | ✅ |
| MN002 | Naming | Banned class suffix (`Manager`, `Helper`, `Utils`) | Warning | ❌ |
| MN003 | Async | `async void` method (not an event handler) | Error | ✅ |
| MN004 | Async | Blocking on async (`.GetAwaiter().GetResult()`, `.Result`, `.Wait()`) | Error | ❌ |
| MN005 | Logging | Direct `ILogger`/`ILogger<T>` call — use `[LoggerMessage]` | Error | ❌ |
| MN006 | Logging | Logging class must be `partial` | Error | ✅ |
| MN007 | Logging | Inject `IAppLogger<T>` instead of `ILogger<T>` | Error | ✅ |
| MN008 | Architecture | Namespace exceeds `MarketNest.<Module>.<Layer>` | Error | ❌ |
| MN009 | Architecture | Use `DateTimeOffset` instead of `DateTime` | Warning | ❌ |
| MN010 | Architecture | Service-locator anti-pattern inside handlers | Error | ❌ |
| MN011 | Async | Public async API missing `CancellationToken` | Warning | ❌ |
| MN012 | Naming | `ICommand<>` class name must end with `Command` | Warning | ❌ |
| MN013 | Naming | `IQuery<>` class name must end with `Query` | Warning | ❌ |
| MN014 | Naming | Handler class name must end with `Handler` | Warning | ❌ |
| MN015 | Naming | Domain event record name must end with `Event` | Warning | ❌ |
| MN016 | Architecture | Entity/Aggregate property must not have public setter | Error | ❌ |
| MN017 | Async | Unnecessary `Task.FromResult(x)` — return directly | Warning | ✅ |
| MN018 | Security | Insecure hash algorithm (MD5, SHA256 — use SHA512+) | Error | ✅ |

> `TreatWarningsAsErrors=true` is set in `Directory.Build.props`, so all warnings also fail the build.

---

## Execution Flow

```
Phase 1: IDENTIFY   → Determine which rule(s) are firing and why
Phase 2: FIX/WRITE  → Apply the correct fix or write the new rule
Phase 3: TEST       → Run analyzer tests to verify
Phase 4: SUPPRESS   → If intentional suppression is needed, apply correctly
```

---

## Phase 1: IDENTIFY — Understand the Violation

### 1.1 Read the diagnostic location

Build errors include the file path and line number:
```
error MN003: Method 'HandleOrder' must return Task, not void
  --> src/MarketNest.Identity/Application/Commands/LoginCommandHandler.cs:42
```

**PowerShell — find all current violations in a module:**
```powershell
dotnet build src/MarketNest.Identity/ --no-incremental 2>&1 |
  Where-Object { $_ -match 'error MN|warning MN' } |
  Sort-Object
```

**Scan entire solution:**
```powershell
dotnet build MarketNest.slnx --no-incremental 2>&1 |
  Where-Object { $_ -match 'MN0[0-9]+' } |
  ForEach-Object { $_ -replace '.*error (MN\d+).*', '$1' } |
  Sort-Object | Group-Object | Select-Object Count, Name
```

### 1.2 Locate analyzer source

```
src/MarketNest.Analyzers/
  Analyzers/
    Naming/         MN001, MN002, MN012, MN013, MN014, MN015
    AsyncRules/     MN003, MN004, MN011, MN017
    Logging/        MN005, MN006, MN007
    Architecture/   MN008, MN009, MN010, MN016, MN018
  CodeFixes/        fixes for MN001, MN003, MN006, MN007, MN017, MN018
  DiagnosticIds.cs  all 18 ID string constants
```

---

## Phase 2: FIX — Rule-by-Rule Fixes

---

### MN001 — Private field naming (`_camelCase`)

```csharp
// ❌ Violates MN001
private readonly IOrderRepository orders;
private ILogger<Handler> Logger;

// ✅ Fixed (Quick Action available)
private readonly IOrderRepository _orders;
private ILogger<Handler> _logger;
```

**Applies to**: `private` fields only. `internal`, `protected`, `public` fields are not checked.

---

### MN002 — Banned class suffix

```csharp
// ❌ Violates MN002
public class OrderManager { }
public class CartHelper { }
public class PaymentUtils { }

// ✅ Fixed — use specific domain names
public class OrderLifecycleService { }  // or split into CommandHandler + domain method
public class CartItemMapper { }         // if purely mapping
public class PaymentFeeCalculator { }   // specific responsibility
```

---

### MN003 — `async void`

```csharp
// ❌ Violates MN003 — exception swallowed, cannot be awaited
public async void HandleOrder() { await Task.Delay(1); }

// ✅ Fixed (Quick Action available)
public async Task HandleOrder() { await Task.Delay(1); }

// ✅ Exemption: event handler signatures with (object, EventArgs) are allowed
public async void OnClick(object sender, EventArgs e) { await DoWorkAsync(); }
```

---

### MN004 — Blocking on async

```csharp
// ❌ Violates MN004 — deadlock risk
var result = GetOrderAsync().Result;
var result = GetOrderAsync().GetAwaiter().GetResult();
GetOrderAsync().Wait();

// ✅ Fixed — propagate async up the call chain
var result = await GetOrderAsync(ct);

// ✅ Acceptable exception: static initializers, console app entry points
// Use #pragma to suppress those specific lines if absolutely necessary
```

---

### MN005 + MN006 + MN007 — Logging pattern

All three rules enforce the `IAppLogger<T>` + `[LoggerMessage]` pattern.

```csharp
// ❌ Violates MN007: injecting ILogger<T> instead of IAppLogger<T>
// ❌ Violates MN006: class is not partial
// ❌ Violates MN005: calling _logger.LogInformation() directly
public class PlaceOrderCommandHandler(ILogger<PlaceOrderCommandHandler> logger)
{
    public async Task<Result<Unit, Error>> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        _logger.LogInformation("Order placed: {OrderId}", cmd.OrderId); // MN005
    }
}

// ✅ Fixed (Quick Actions available for MN006 and MN007)
public partial class PlaceOrderCommandHandler(IAppLogger<PlaceOrderCommandHandler> logger)
{
    public async Task<Result<Unit, Error>> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        Log.OrderPlaced(logger, cmd.OrderId);
        // ...
    }

    private static partial class Log
    {
        [LoggerMessage(LogEventId.Orders + 1, LogLevel.Information, "Order placed: {OrderId}")]
        public static partial void OrderPlaced(ILogger logger, Guid orderId);
    }
}
```

**Key rules:**
- `IAppLogger<T>` is defined in `Base.Infrastructure/Logging/` — import that namespace
- `LogEventId` enum in the same package — each module owns a block of 10,000 IDs (e.g., Orders = 40000–49999)
- `[LoggerMessage]` attribute must be on a `partial void` method in a nested `private static partial class Log`

---

### MN008 — Flat namespace

```csharp
// ❌ Violates MN008 — sub-folder in namespace
namespace MarketNest.Identity.Application.Commands;
namespace MarketNest.Orders.Domain.Entities;
namespace MarketNest.Catalog.Infrastructure.Persistence;

// ✅ Fixed — stop at layer level
namespace MarketNest.Identity.Application;
namespace MarketNest.Orders.Domain;
namespace MarketNest.Catalog.Infrastructure;
```

**Known exemption**: `MarketNest.Web` is suppressed in `.csproj` because Razor Pages use
folder-matched namespaces required by the `@model` directive.

---

### MN009 — `DateTimeOffset` over `DateTime`

```csharp
// ❌ Violates MN009 — timezone-unsafe
public DateTime CreatedAt { get; private set; }
var now = DateTime.UtcNow;

// ✅ Fixed
public DateTimeOffset CreatedAt { get; private set; }
var now = DateTimeOffset.UtcNow;
```

**Suppress only** when integrating with a third-party library type that requires `DateTime`.

---

### MN010 — Service locator anti-pattern

```csharp
// ❌ Violates MN010 — service locator inside a handler
public class MyHandler(IServiceProvider sp)
{
    public async Task<Result<Unit, Error>> Handle(...)
    {
        var repo = sp.GetRequiredService<IOrderRepository>(); // service locator
    }
}

// ✅ Fixed — inject the dependency directly
public class MyHandler(IOrderRepository repo) { }
```

---

### MN011 — Missing `CancellationToken`

```csharp
// ❌ Violates MN011
public async Task<Order?> GetByIdAsync(Guid id) // missing ct
    => await db.Orders.FirstOrDefaultAsync(o => o.Id == id);

// ✅ Fixed
public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
    => await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
```

**Note**: Only fires on `public` methods. Internal helpers are exempt.

---

### MN012 + MN013 + MN014 + MN015 — CQRS naming

```csharp
// ❌ Violates MN012 — ICommand<> implementor not ending with "Command"
public record PlaceOrder : ICommand<PlaceOrderResult> { }

// ❌ Violates MN013 — IQuery<> implementor not ending with "Query"
public record FetchOrderById(Guid Id) : IQuery<OrderDetailDto?> { }

// ❌ Violates MN014 — handler not ending with "Handler"
public class PlaceOrderProcessor : ICommandHandler<PlaceOrderCommand, Unit> { }

// ❌ Violates MN015 — domain event record not ending with "Event"
public record OrderCompleted(Guid Id) : IDomainEvent { }

// ✅ Fixed
public record PlaceOrderCommand : ICommand<PlaceOrderResult> { }
public record GetOrderByIdQuery(Guid Id) : IQuery<OrderDetailDto?> { }
public class PlaceOrderCommandHandler : ICommandHandler<PlaceOrderCommand, Unit> { }
public record OrderCompletedEvent(Guid Id) : IDomainEvent { }
```

---

### MN016 — Entity/Aggregate public setter

```csharp
// ❌ Violates MN016 — anemic domain model, invariant bypass risk
public class Order : AggregateRoot
{
    public OrderStatus Status { get; set; }   // MN016
    public string Address { get; set; }       // MN016
}

// ✅ Fixed — private setter + domain method guards invariants
public class Order : AggregateRoot
{
    public OrderStatus Status { get; private set; }
    public string Address { get; private set; }

    public Result<Unit, Error> ChangeAddress(string newAddress)
    {
        if (Status == OrderStatus.Shipped)
            return Errors.Order.CannotChangeAddressAfterShipment;
        Address = newAddress;
        return Result.Success();
    }
}
```

**Note**: Only fires on classes that inherit `Entity<T>` or `AggregateRoot`. Plain DTOs and records are not checked.

---

### MN017 — Unnecessary `Task.FromResult`

```csharp
// ❌ Violates MN017 — unnecessary wrapping
public async Task<int> GetCountAsync() => await Task.FromResult(42);
public Task<string> GetNameAsync() => Task.FromResult("test");

// ✅ Fixed (Quick Action available)
public int GetCount() => 42;           // drop async if no I/O
public ValueTask<string> GetNameAsync() => ValueTask.FromResult("test"); // if async return needed
```

---

### MN018 — Insecure hash algorithm

```csharp
// ❌ Violates MN018 — weak algorithm
using System.Security.Cryptography;
var hash = MD5.HashData(data);
var hash2 = SHA256.HashData(data);

// ✅ Fixed (Quick Action available)
using System.Security.Cryptography;
var hash = SHA512.HashData(data);
```

---

## Phase 3: TEST — Writing Analyzer Tests

Tests live in `tests/MarketNest.Analyzers.Tests/`. One file per analyzer.

### Test file structure

```csharp
// tests/MarketNest.Analyzers.Tests/Naming/PrivateFieldNamingAnalyzerTests.cs
using MarketNest.Analyzers.Naming;
using MarketNest.Analyzers.CodeFixes;
using Xunit;

namespace MarketNest.Analyzers.Tests.Naming;

public class PrivateFieldNamingAnalyzerTests
{
    // ── trigger test ──────────────────────────────────────────────────────
    [Fact]
    public async Task Triggers_for_private_field_without_underscore()
    {
        var source = """
            class C {
                private int {|MN001:count|};  // ← span marks the diagnostic location
            }
            """;
        await Verify<PrivateFieldNamingAnalyzer>.AnalyzerAsync(source);
    }

    // ── no-trigger test ───────────────────────────────────────────────────
    [Fact]
    public async Task No_trigger_for_correctly_named_field()
    {
        var source = """
            class C {
                private int _count;  // ← no diagnostic expected
            }
            """;
        await Verify<PrivateFieldNamingAnalyzer>.AnalyzerAsync(source);
    }

    // ── code fix test ─────────────────────────────────────────────────────
    [Fact]
    public async Task CodeFix_adds_underscore_prefix()
    {
        var source = """
            class C {
                private int {|MN001:count|};
            }
            """;
        var fixedSource = """
            class C {
                private int _count;
            }
            """;
        await VerifyFix<PrivateFieldNamingAnalyzer, PrivateFieldNamingCodeFix>
            .CodeFixAsync(source, fixedSource);
    }
}
```

### Test helper: `Verify<T>` and `VerifyFix<TAnalyzer, TFix>`

These helpers wrap `Microsoft.CodeAnalysis.CSharp.Testing`. They are defined in
`tests/MarketNest.Analyzers.Tests/Verify.cs`. Usage:

```csharp
// Analyzer-only test
await Verify<MyAnalyzer>.AnalyzerAsync(source);

// Code fix test
await VerifyFix<MyAnalyzer, MyCodeFix>.CodeFixAsync(source, fixedSource);
```

**Diagnostic span syntax**: wrap the problematic token in `{|MN0xx:token|}`. The test
framework asserts that exactly this diagnostic fires at this location.

### Run analyzer tests

```powershell
dotnet test tests/MarketNest.Analyzers.Tests/ -v normal
```

**Expected**: all 73+ tests green. Zero failures.

---

## Phase 4: SUPPRESS — Intentional Suppressions

Use `#pragma` for file-level suppression. Always add a comment explaining **why**:

```csharp
// ✅ Correct suppression with reason
#pragma warning disable MN009 // DateTimeOffset not applicable — EF migration timestamp
public DateTime CreatedAt { get; set; }
#pragma warning restore MN009
```

**Known intentional suppressions** (do not remove):

| Location | Rule | Reason |
|----------|------|--------|
| `AppLogger.cs` | MN007 | `AppLogger<T>` IS the `IAppLogger` implementation — must accept `ILogger<T>` |
| `NpgsqlJobExecutionStore.cs` | MN004 | Constructor cannot be `async` — one-time idempotent DDL bootstrap |
| `MarketNest.Web.csproj` | MN008 | Razor Pages use folder-matched namespaces; flat namespaces cause `@model` collisions |

### Global suppression via `.editorconfig`

Only for very specific files, using `dotnet_diagnostic.MNxxx.severity = none`. Prefer `#pragma`
for inline suppressions.

---

## Adding a New Analyzer Rule

### Step-by-step

1. **Add ID constant** in `src/MarketNest.Analyzers/DiagnosticIds.cs`:
   ```csharp
   public const string MN019 = "MN019";
   ```

2. **Create analyzer** in `src/MarketNest.Analyzers/Analyzers/{Category}/MyAnalyzer.cs`:
   ```csharp
   [DiagnosticAnalyzer(LanguageNames.CSharp)]
   public class MyAnalyzer : DiagnosticAnalyzer
   {
       private static readonly DiagnosticDescriptor Rule = new(
           id:                 DiagnosticIds.MN019,
           title:             "My rule title",
           messageFormat:     "My rule message: {0}",
           category:          "Architecture",
           defaultSeverity:   DiagnosticSeverity.Error,
           isEnabledByDefault: true,
           description:       "Detailed description for docs.");

       public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
           => [Rule];

       public override void Initialize(AnalysisContext context)
       {
           context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
           context.EnableConcurrentExecution();
           context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.MethodDeclaration);
       }

       private static void AnalyzeNode(SyntaxNodeAnalysisContext ctx) { /* ... */ }
   }
   ```

3. **Create code fix** (optional) in `src/MarketNest.Analyzers/CodeFixes/MyCodeFix.cs`.

4. **Write tests** in `tests/MarketNest.Analyzers.Tests/{Category}/MyAnalyzerTests.cs`:
   - At minimum: one trigger test, one no-trigger test
   - If code fix: one `CodeFixAsync` test

5. **Update docs**: Add a row to the table in `docs/analyzers.md`.

6. **Verify**:
   ```powershell
   dotnet test tests/MarketNest.Analyzers.Tests/
   dotnet build MarketNest.slnx
   ```

---

## Common Mistakes When Writing Analyzers

```csharp
// ❌ Forgetting ConfigureGeneratedCodeAnalysis — may fire on generated code
public override void Initialize(AnalysisContext context)
{
    context.EnableConcurrentExecution();
    // missing: context.ConfigureGeneratedCodeAnalysis(...)
    context.RegisterSyntaxNodeAction(...)
}

// ❌ Using Contains on SymbolKind instead of IsKind
if (symbol.Kind.Contains(SymbolKind.Method)) // wrong: SymbolKind is not a collection!
if (symbol.Kind == SymbolKind.Method)         // ✅ correct

// ❌ Reporting diagnostic at wrong location — squiggly appears in wrong place
context.ReportDiagnostic(Diagnostic.Create(Rule, classDecl.GetLocation())); // entire class
context.ReportDiagnostic(Diagnostic.Create(Rule, classDecl.Identifier.GetLocation())); // ✅ just the name

// ❌ Not covering partial classes (IMethodSymbol spans multiple declarations)
// Always iterate symbol.DeclaringSyntaxReferences if checking class-level attributes
```

---

## Quick Scan — Find All Violations

```powershell
# Full build + capture analyzer output
dotnet build MarketNest.slnx --no-incremental 2>&1 |
  Where-Object { $_ -match '(error|warning) MN\d+' } |
  ForEach-Object { ($_ -split ':')[0..2] -join ':' } |
  Sort-Object | Get-Unique

# Count violations per rule
dotnet build MarketNest.slnx --no-incremental 2>&1 |
  Select-String -Pattern 'MN\d+' |
  ForEach-Object { ($_.Matches[0].Value) } |
  Group-Object | Sort-Object Count -Descending | Format-Table Count, Name
```

