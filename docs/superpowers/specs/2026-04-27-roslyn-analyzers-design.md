# MarketNest Roslyn Analyzers — Design Spec

> Date: 2026-04-27 | Status: Approved | Author: Tran Nha Hoang

## Overview

A custom Roslyn analyzer project (`MarketNest.Analyzers`) that automatically enforces the code rules defined in `docs/code-rules.md`. It lives inside the existing solution, produces build-time diagnostics in the IDE and CI, and ships 5 code fix providers for the simplest violations.

## Goals

- Enforce 17 rules from `docs/code-rules.md` / §11 PR checklist at build time
- Surface violations as IDE squiggly lines and CI build errors
- Provide auto-fix via Quick Actions for 5 simple rules
- Keep the analyzer project co-located in the solution (no NuGet publish overhead)
- Each rule independently testable and independently disable-able

## Out of Scope

- Source generation (generating logging boilerplate)
- CSS / frontend rules (Roslyn does not analyze `.css`/`.cshtml` style)
- Runtime enforcement (analyzers are build-time only)
- Code fixes for complex rules (namespace rename, entity setter, CancellationToken injection)

---

## Project Structure

```
src/
  MarketNest.Analyzers/
    Analyzers/
      Naming/
        PrivateFieldNamingAnalyzer.cs
        BannedClassSuffixAnalyzer.cs
        CommandQueryNamingAnalyzer.cs
      AsyncRules/
        AsyncVoidAnalyzer.cs
        BlockingAsyncAnalyzer.cs
        TaskFromResultAnalyzer.cs
        CancellationTokenAnalyzer.cs
      Logging/
        DirectLoggerCallAnalyzer.cs
        LoggingClassPartialAnalyzer.cs
        AppLoggerInjectionAnalyzer.cs
      Architecture/
        FlatNamespaceAnalyzer.cs
        DateTimeUsageAnalyzer.cs
        ServiceLocatorAnalyzer.cs
        EntityPublicSetterAnalyzer.cs
    CodeFixes/
      PrivateFieldNamingCodeFix.cs
      AsyncVoidCodeFix.cs
      LoggingClassPartialCodeFix.cs
      AppLoggerInjectionCodeFix.cs
      TaskFromResultCodeFix.cs
    DiagnosticIds.cs
    MarketNest.Analyzers.csproj

tests/
  MarketNest.Analyzers.Tests/
    Naming/
      PrivateFieldNamingAnalyzerTests.cs
      BannedClassSuffixAnalyzerTests.cs
      CommandQueryNamingAnalyzerTests.cs
    AsyncRules/
      AsyncVoidAnalyzerTests.cs
      BlockingAsyncAnalyzerTests.cs
      TaskFromResultAnalyzerTests.cs
      CancellationTokenAnalyzerTests.cs
    Logging/
      DirectLoggerCallAnalyzerTests.cs
      LoggingClassPartialAnalyzerTests.cs
      AppLoggerInjectionAnalyzerTests.cs
    Architecture/
      FlatNamespaceAnalyzerTests.cs
      DateTimeUsageAnalyzerTests.cs
      ServiceLocatorAnalyzerTests.cs
      EntityPublicSetterAnalyzerTests.cs
    MarketNest.Analyzers.Tests.csproj
```

### Integration into Solution

Every module `.csproj` references the analyzer project with `OutputItemType="Analyzer"` and `ReferenceOutputAssembly="false"` so the assembly is not deployed:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\MarketNest.Analyzers\MarketNest.Analyzers.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

This reference will be added to a shared `Directory.Build.props` fragment or manually to each module `.csproj`.

### `.csproj` Configuration

`MarketNest.Analyzers.csproj` targets `netstandard2.0` (required by Roslyn analyzer SDK) and must **not** inherit the root `Directory.Build.props` `net10.0` target. This is achieved by placing a local `src/MarketNest.Analyzers/Directory.Build.props` that overrides `TargetFramework` to `netstandard2.0` and sets `<IsRoslynComponent>true</IsRoslynComponent>`. Package references (versions pinned in root `Directory.Packages.props`):

```xml
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" />
<PackageReference Include="Microsoft.CodeAnalysis.Analyzers">
  <PrivateAssets>all</PrivateAssets>
  <IncludeAssets>runtime; build; native; contentfiles; analyzers</IncludeAssets>
</PackageReference>
```

`MarketNest.Analyzers.Tests.csproj` targets `net10.0` and references (versions pinned in root `Directory.Packages.props`):

```xml
<PackageReference Include="Microsoft.CodeAnalysis.CSharp.Testing" />
<PackageReference Include="Microsoft.CodeAnalysis.Testing" />
<PackageReference Include="xunit" />
```

---

## Diagnostic IDs

All IDs declared as constants in `DiagnosticIds.cs`:

| ID | Title | Severity | Code Fix |
|----|-------|----------|----------|
| MN001 | PrivateFieldMustUseCamelCaseWithUnderscore | Error | ✅ |
| MN002 | BannedClassSuffix | Warning | ❌ |
| MN003 | AsyncVoidMethod | Error | ✅ |
| MN004 | BlockingAsyncCall | Error | ❌ |
| MN005 | DirectLoggerCall | Error | ❌ |
| MN006 | LoggingClassMustBePartial | Error | ✅ |
| MN007 | MustInjectAppLoggerNotILogger | Error | ✅ |
| MN008 | NamespaceMustBeFlatLayerLevel | Error | ❌ |
| MN009 | DateTimeMustUseDateTimeOffset | Warning | ❌ |
| MN010 | ServiceLocatorAntiPattern | Error | ❌ |
| MN011 | PublicAsyncApiMissingCancellationToken | Warning | ❌ |
| MN012 | CommandClassNamingConvention | Warning | ❌ |
| MN013 | QueryClassNamingConvention | Warning | ❌ |
| MN014 | HandlerClassNamingConvention | Warning | ❌ |
| MN015 | EventRecordNamingConvention | Warning | ❌ |
| MN016 | EntityAggregatePropertyMustNotHavePublicSetter | Error | ❌ |
| MN017 | UnnecessaryTaskFromResult | Warning | ✅ |

> `TreatWarningsAsErrors=true` is active in `Directory.Build.props`, so all warnings also fail the build. Individual rules can be suppressed with `#pragma warning disable MNxxx` when genuinely needed.

---

## Analyzer Details

### Naming

**MN001 PrivateFieldNamingAnalyzer**
- Register: `SyntaxKind.FieldDeclaration`
- Trigger: field is `private` (or `private readonly`) AND name does not start with `_` followed by lowercase letter
- Skip: `const` fields (different convention), `static readonly` fields used as constants

**MN002 BannedClassSuffixAnalyzer**
- Register: `SyntaxKind.ClassDeclaration`
- Trigger: class name ends with `Manager`, `Helper`, or `Utils` (case-insensitive)

**MN012–MN015 CommandQueryNamingAnalyzer** (one analyzer, four descriptors)
- Register: `SyntaxKind.ClassDeclaration`, `SyntaxKind.RecordDeclaration`
- Detect implementing `ICommand<>` → name must end with `Command` (MN012)
- Detect implementing `IQuery<>` → name must start with `Get` and end with `Query` (MN013)
- Detect implementing `ICommandHandler<,>` or `IQueryHandler<,>` → name must end with `Handler` (MN014)
- Detect implementing `IDomainEvent` or `IIntegrationEvent` → name must end with `Event` (MN015)

### AsyncRules

**MN003 AsyncVoidAnalyzer**
- Register: `SyntaxKind.MethodDeclaration`
- Trigger: method has `async` modifier + return type `void`
- Skip: method has a parameter of type `EventArgs` or derived (genuine event handler)

**MN004 BlockingAsyncAnalyzer**
- Register: `SyntaxKind.MemberAccessExpression`
- Trigger: `.Result` or `.GetAwaiter().GetResult()` accessed on an expression whose type is `Task`, `Task<T>`, or `ValueTask<T>`

**MN011 CancellationTokenAnalyzer**
- Register: `SyntaxKind.MethodDeclaration`
- Trigger: method is `public` or `protected` AND (is `async` OR return type is `Task`/`ValueTask`) AND no `CancellationToken` parameter present
- Scope: only interface member declarations and abstract class methods (avoids false positives on concrete overrides that already satisfy the interface)

**MN017 TaskFromResultAnalyzer**
- Register: `SyntaxKind.AwaitExpression`
- Trigger: the awaited expression is an invocation of `Task.FromResult(...)`

### Logging

**MN005 DirectLoggerCallAnalyzer**
- Register: `SyntaxKind.InvocationExpression`
- Trigger: method name matches `Log{Level}` pattern (LogInformation, LogWarning, LogError, LogDebug, LogCritical, LogTrace) called on a symbol of type `ILogger<T>` or `IAppLogger<T>`

**MN006 LoggingClassPartialAnalyzer**
- Register: `SyntaxKind.ClassDeclaration`
- Trigger: class has a constructor parameter or field of type `IAppLogger<T>` AND class declaration does not have `partial` modifier

**MN007 AppLoggerInjectionAnalyzer**
- Register: `SyntaxKind.Parameter` (primary constructor parameters) and `SyntaxKind.FieldDeclaration`
- Trigger: declared type is `ILogger<T>` (from `Microsoft.Extensions.Logging`) rather than `IAppLogger<T>`

### Architecture

**MN008 FlatNamespaceAnalyzer**
- Register: `SyntaxKind.NamespaceDeclaration`, `SyntaxKind.FileScopedNamespaceDeclaration`
- Trigger: namespace has more than 3 dot-separated segments (e.g., `MarketNest.Orders.Application.Commands` = 4 segments → trigger). Threshold is 3 because `MarketNest.<Module>.<Layer>` is the allowed maximum.
- Skip: namespaces not starting with `MarketNest.` (third-party or generated code)

**MN009 DateTimeUsageAnalyzer**
- Register: `SyntaxKind.PropertyDeclaration`, `SyntaxKind.FieldDeclaration`
- Trigger: declared type is `System.DateTime` or `System.DateTime?`
- Skip: files in test projects (heuristic: project name ends with `Tests`)

**MN010 ServiceLocatorAnalyzer**
- Register: `SyntaxKind.InvocationExpression`
- Trigger: call to `GetService<T>()` or `GetRequiredService<T>()` inside a class that implements `ICommandHandler<,>`, `IQueryHandler<,>`, or inherits `PageModel`

**MN016 EntityPublicSetterAnalyzer**
- Register: `SyntaxKind.PropertyDeclaration`
- Trigger: property has `public set;` or implicit public setter AND containing class inherits from `Entity<>` or `AggregateRoot`
- Skip: properties whose type implements `ISoftDeletable` or `IAuditable` (infrastructure interfaces need `set`)

---

## Code Fix Details

### MN001 — PrivateFieldNamingCodeFix
Uses `Renamer.RenameSymbolAsync` to rename the field symbol across the document. Transforms:
- `count` → `_count`
- `Name` → `_name` (lowercase first char, prepend `_`)
- `m_count` → `_count` (strip `m_` prefix, prepend `_`)

### MN003 — AsyncVoidCodeFix
Replaces the `void` return type token with `Task`. Adds `using System.Threading.Tasks;` directive if not already present.

### MN006 — LoggingClassPartialCodeFix
Inserts `partial` modifier token into the `ClassDeclarationSyntax` modifier list, between the accessibility modifier and the `class` keyword.

### MN007 — AppLoggerInjectionCodeFix
Replaces the `ILogger<T>` type syntax node with `IAppLogger<T>`. Adds the using directive for `MarketNest.Base.Infrastructure.Logging` if not present.

### MN017 — TaskFromResultCodeFix
Removes the `await` keyword and unwraps `Task.FromResult(value)` → returns the `value` expression directly. Does not change the method return type or remove `async` modifier (avoids cascading changes that could break other awaits in the same method).

---

## Test Strategy

Each analyzer has a corresponding test class using `CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>` from `Microsoft.CodeAnalysis.CSharp.Testing`. Each test class contains:

1. **Trigger test** — inline C# source with `{|MNxxx: ... |}` markers asserting the diagnostic fires at the correct location
2. **No-trigger test** — inline C# source for the valid/compliant case asserting zero diagnostics
3. **Edge case tests** — e.g., `const` field skipped by MN001, EventHandler skipped by MN003

Code fix tests use `CSharpCodeFixTest<TAnalyzer, TCodeFix, DefaultVerifier>` with `TestCode` and `FixedCode` properties.

---

## Build & CI Impact

- `MarketNest.Analyzers` is `netstandard2.0` only — excluded from `Directory.Build.props` `net10.0` target via `<Import Condition="'$(MSBuildProjectName)' != 'MarketNest.Analyzers'">` or a local `Directory.Build.props` override in `src/MarketNest.Analyzers/`.
- All module projects gain a build-time analyzer reference; no runtime overhead.
- Existing `CA1848` suppression in root `Directory.Build.props` (`<NoWarn>$(NoWarn);CA1848</NoWarn>`) remains — MN005 is the project-specific replacement.
- CI pipeline (`dotnet build`) automatically runs analyzers; no additional step needed.
