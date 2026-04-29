# MarketNest Roslyn Analyzers Reference

> Design spec: `docs/superpowers/specs/2026-04-27-roslyn-analyzers-design.md`
> Status: **Updated** (2026-04-29) — all 18 rules + 6 code fixes implemented, wired to all src/ projects

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

> `TreatWarningsAsErrors=true` is set in `Directory.Build.props`, so all warnings also fail the build.

---

## Project Structure

```
src/MarketNest.Analyzers/
  Analyzers/
    Naming/            PrivateFieldNamingAnalyzer, BannedClassSuffixAnalyzer, CommandQueryNamingAnalyzer
    AsyncRules/        AsyncVoidAnalyzer, BlockingAsyncAnalyzer, TaskFromResultAnalyzer, CancellationTokenAnalyzer
    Logging/           DirectLoggerCallAnalyzer, LoggingClassPartialAnalyzer, AppLoggerInjectionAnalyzer
    Architecture/      FlatNamespaceAnalyzer, DateTimeUsageAnalyzer, ServiceLocatorAnalyzer, EntityPublicSetterAnalyzer, InsecureHashAnalyzer
  CodeFixes/           PrivateFieldNamingCodeFix, AsyncVoidCodeFix, LoggingClassPartialCodeFix,
                       AppLoggerInjectionCodeFix, TaskFromResultCodeFix, InsecureHashCodeFix
  DiagnosticIds.cs     All 18 ID constants

tests/MarketNest.Analyzers.Tests/
  Naming/ AsyncRules/ Logging/ Architecture/   — one test class per analyzer (74+ tests total)
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

---

## Testing

Run analyzer tests in isolation:
```bash
dotnet test tests/MarketNest.Analyzers.Tests/
```

All 73 tests should pass. Each test class uses the `Microsoft.CodeAnalysis.CSharp.Testing` framework with inline source markup:
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
