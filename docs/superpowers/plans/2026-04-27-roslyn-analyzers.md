# MarketNest Roslyn Analyzers — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `MarketNest.Analyzers` — 17 custom Roslyn diagnostic rules (MN001–MN017) enforcing `docs/code-rules.md`, with 5 code fix providers for auto-fixable violations.

**Architecture:** One `DiagnosticAnalyzer` per rule in four categories (Naming / AsyncRules / Logging / Architecture). Five `CodeFixProvider` classes for MN001, MN003, MN006, MN007, MN017. One test class per analyzer. Analyzer project targets `netstandard2.0`, wired to `src/` projects via `src/Directory.Build.targets` (added in the final task).

**Tech Stack:** `Microsoft.CodeAnalysis.CSharp` 4.x, `Microsoft.CodeAnalysis.Analyzers` 3.x, `Microsoft.CodeAnalysis.CSharp.Testing` 1.1.x, xUnit. Spec: `docs/superpowers/specs/2026-04-27-roslyn-analyzers-design.md`.

---

## File Map

**New — Analyzer project**
- `src/MarketNest.Analyzers/Directory.Build.props` — override `netstandard2.0`, opt out of root net10.0 target
- `src/MarketNest.Analyzers/MarketNest.Analyzers.csproj`
- `src/MarketNest.Analyzers/DiagnosticIds.cs` — all 17 `const string` IDs
- `src/MarketNest.Analyzers/Analyzers/Naming/PrivateFieldNamingAnalyzer.cs` (MN001)
- `src/MarketNest.Analyzers/Analyzers/Naming/BannedClassSuffixAnalyzer.cs` (MN002)
- `src/MarketNest.Analyzers/Analyzers/Naming/CommandQueryNamingAnalyzer.cs` (MN012–MN015)
- `src/MarketNest.Analyzers/Analyzers/AsyncRules/AsyncVoidAnalyzer.cs` (MN003)
- `src/MarketNest.Analyzers/Analyzers/AsyncRules/BlockingAsyncAnalyzer.cs` (MN004)
- `src/MarketNest.Analyzers/Analyzers/AsyncRules/TaskFromResultAnalyzer.cs` (MN017)
- `src/MarketNest.Analyzers/Analyzers/AsyncRules/CancellationTokenAnalyzer.cs` (MN011)
- `src/MarketNest.Analyzers/Analyzers/Logging/DirectLoggerCallAnalyzer.cs` (MN005)
- `src/MarketNest.Analyzers/Analyzers/Logging/LoggingClassPartialAnalyzer.cs` (MN006)
- `src/MarketNest.Analyzers/Analyzers/Logging/AppLoggerInjectionAnalyzer.cs` (MN007)
- `src/MarketNest.Analyzers/Analyzers/Architecture/FlatNamespaceAnalyzer.cs` (MN008)
- `src/MarketNest.Analyzers/Analyzers/Architecture/DateTimeUsageAnalyzer.cs` (MN009)
- `src/MarketNest.Analyzers/Analyzers/Architecture/ServiceLocatorAnalyzer.cs` (MN010)
- `src/MarketNest.Analyzers/Analyzers/Architecture/EntityPublicSetterAnalyzer.cs` (MN016)
- `src/MarketNest.Analyzers/CodeFixes/PrivateFieldNamingCodeFix.cs`
- `src/MarketNest.Analyzers/CodeFixes/AsyncVoidCodeFix.cs`
- `src/MarketNest.Analyzers/CodeFixes/LoggingClassPartialCodeFix.cs`
- `src/MarketNest.Analyzers/CodeFixes/AppLoggerInjectionCodeFix.cs`
- `src/MarketNest.Analyzers/CodeFixes/TaskFromResultCodeFix.cs`

**New — Test project**
- `tests/MarketNest.Analyzers.Tests/MarketNest.Analyzers.Tests.csproj`
- `tests/MarketNest.Analyzers.Tests/TestHelpers.cs` — shared `Verify<T>` / `VerifyFix<T,F>` helpers
- `tests/MarketNest.Analyzers.Tests/Naming/PrivateFieldNamingAnalyzerTests.cs`
- `tests/MarketNest.Analyzers.Tests/Naming/BannedClassSuffixAnalyzerTests.cs`
- `tests/MarketNest.Analyzers.Tests/Naming/CommandQueryNamingAnalyzerTests.cs`
- `tests/MarketNest.Analyzers.Tests/AsyncRules/AsyncVoidAnalyzerTests.cs`
- `tests/MarketNest.Analyzers.Tests/AsyncRules/BlockingAsyncAnalyzerTests.cs`
- `tests/MarketNest.Analyzers.Tests/AsyncRules/TaskFromResultAnalyzerTests.cs`
- `tests/MarketNest.Analyzers.Tests/AsyncRules/CancellationTokenAnalyzerTests.cs`
- `tests/MarketNest.Analyzers.Tests/Logging/DirectLoggerCallAnalyzerTests.cs`
- `tests/MarketNest.Analyzers.Tests/Logging/LoggingClassPartialAnalyzerTests.cs`
- `tests/MarketNest.Analyzers.Tests/Logging/AppLoggerInjectionAnalyzerTests.cs`
- `tests/MarketNest.Analyzers.Tests/Architecture/FlatNamespaceAnalyzerTests.cs`
- `tests/MarketNest.Analyzers.Tests/Architecture/DateTimeUsageAnalyzerTests.cs`
- `tests/MarketNest.Analyzers.Tests/Architecture/ServiceLocatorAnalyzerTests.cs`
- `tests/MarketNest.Analyzers.Tests/Architecture/EntityPublicSetterAnalyzerTests.cs`

**Modified**
- `Directory.Packages.props` — add Roslyn + testing package versions
- `MarketNest.slnx` — add both new projects
- `src/Directory.Build.targets` — **created in Task 16** to auto-wire analyzer to all src/ projects

---

## Task 1: Scaffold both projects

**Files:**
- Create: `src/MarketNest.Analyzers/Directory.Build.props`
- Create: `src/MarketNest.Analyzers/MarketNest.Analyzers.csproj`
- Create: `src/MarketNest.Analyzers/DiagnosticIds.cs`
- Create: `tests/MarketNest.Analyzers.Tests/MarketNest.Analyzers.Tests.csproj`
- Create: `tests/MarketNest.Analyzers.Tests/TestHelpers.cs`
- Modify: `Directory.Packages.props`
- Modify: `MarketNest.slnx`

- [ ] **Step 1: Create `src/MarketNest.Analyzers/Directory.Build.props`**

This overrides the root `Directory.Build.props` (which targets `net10.0`) for the analyzer project only. It imports the parent first to inherit shared settings, then overrides the target framework.

```xml
<Project>
  <!-- Import root Directory.Build.props to inherit shared settings -->
  <Import Project="$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory)..\, 'Directory.Build.props'))\Directory.Build.props"
          Condition="'$([MSBuild]::GetDirectoryNameOfFileAbove($(MSBuildThisFileDirectory)..\, Directory.Build.props))' != ''" />
  <PropertyGroup>
    <!-- Roslyn analyzers must target netstandard2.0 -->
    <TargetFramework>netstandard2.0</TargetFramework>
    <IsRoslynComponent>true</IsRoslynComponent>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

- [ ] **Step 2: Create `src/MarketNest.Analyzers/MarketNest.Analyzers.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <Description>Custom Roslyn analyzers enforcing MarketNest code rules (MN001–MN017).</Description>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Add Roslyn package versions to `Directory.Packages.props`**

Open `Directory.Packages.props` and add a new `<ItemGroup Label="Roslyn Analyzers">` section (check latest versions on NuGet.org for `Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.CSharp.Testing` before pinning):

```xml
<ItemGroup Label="Roslyn Analyzers">
  <PackageVersion Include="Microsoft.CodeAnalysis.CSharp" Version="4.13.0" />
  <PackageVersion Include="Microsoft.CodeAnalysis.Analyzers" Version="3.11.0" />
  <PackageVersion Include="Microsoft.CodeAnalysis.CSharp.Testing" Version="1.1.2" />
</ItemGroup>
```

> Verify actual latest stable versions at https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp before committing.

- [ ] **Step 4: Create `src/MarketNest.Analyzers/DiagnosticIds.cs`**

```csharp
namespace MarketNest.Analyzers;

internal static class DiagnosticIds
{
    public const string MN001 = "MN001";
    public const string MN002 = "MN002";
    public const string MN003 = "MN003";
    public const string MN004 = "MN004";
    public const string MN005 = "MN005";
    public const string MN006 = "MN006";
    public const string MN007 = "MN007";
    public const string MN008 = "MN008";
    public const string MN009 = "MN009";
    public const string MN010 = "MN010";
    public const string MN011 = "MN011";
    public const string MN012 = "MN012";
    public const string MN013 = "MN013";
    public const string MN014 = "MN014";
    public const string MN015 = "MN015";
    public const string MN016 = "MN016";
    public const string MN017 = "MN017";
}
```

- [ ] **Step 5: Create `tests/MarketNest.Analyzers.Tests/MarketNest.Analyzers.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp.Testing" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\MarketNest.Analyzers\MarketNest.Analyzers.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Create `tests/MarketNest.Analyzers.Tests/TestHelpers.cs`**

Shared helpers used by every test class in this project.

```csharp
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace MarketNest.Analyzers.Tests;

internal static class Verify<TAnalyzer> where TAnalyzer : DiagnosticAnalyzer, new()
{
    public static Task AnalyzerAsync(string source)
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier> { TestCode = source };
        return test.RunAsync();
    }
}

internal static class VerifyFix<TAnalyzer, TFix>
    where TAnalyzer : DiagnosticAnalyzer, new()
    where TFix : CodeFixProvider, new()
{
    public static Task CodeFixAsync(string source, string fixedSource)
    {
        var test = new CSharpCodeFixTest<TAnalyzer, TFix, DefaultVerifier>
        {
            TestCode = source,
            FixedCode = fixedSource
        };
        return test.RunAsync();
    }
}
```

- [ ] **Step 7: Add both projects to `MarketNest.slnx`**

Open `MarketNest.slnx` and add:
- Inside `<Folder Name="/src/">`: `<Project Path="src/MarketNest.Analyzers/MarketNest.Analyzers.csproj" />`
- Inside `<Folder Name="/tests/">`: `<Project Path="tests/MarketNest.Analyzers.Tests/MarketNest.Analyzers.Tests.csproj" />`

- [ ] **Step 8: Verify build**

```bash
dotnet build src/MarketNest.Analyzers/MarketNest.Analyzers.csproj
dotnet build tests/MarketNest.Analyzers.Tests/MarketNest.Analyzers.Tests.csproj
```

Both should produce `Build succeeded` with 0 errors.

- [ ] **Step 9: Commit**

```bash
git add src/MarketNest.Analyzers/ tests/MarketNest.Analyzers.Tests/ Directory.Packages.props MarketNest.slnx
git commit -m "chore(analyzers): scaffold MarketNest.Analyzers and test projects"
```

---

## Task 2: MN001 — Private field naming (`_camelCase`)

**Files:**
- Create: `src/MarketNest.Analyzers/Analyzers/Naming/PrivateFieldNamingAnalyzer.cs`
- Create: `src/MarketNest.Analyzers/CodeFixes/PrivateFieldNamingCodeFix.cs`
- Create: `tests/MarketNest.Analyzers.Tests/Naming/PrivateFieldNamingAnalyzerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MarketNest.Analyzers.Tests/Naming/PrivateFieldNamingAnalyzerTests.cs
using MarketNest.Analyzers.Naming;
using Xunit;

namespace MarketNest.Analyzers.Tests.Naming;

public class PrivateFieldNamingAnalyzerTests
{
    [Fact]
    public async Task Triggers_when_private_field_has_no_underscore()
    {
        var source = """
            class C {
                private int {|MN001:count|};
            }
            """;
        await Verify<PrivateFieldNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_when_private_field_uses_PascalCase()
    {
        var source = """
            class C {
                private string {|MN001:Name|};
            }
            """;
        await Verify<PrivateFieldNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_valid_underscore_camelCase()
    {
        var source = """
            class C {
                private int _count;
                private readonly string _name;
            }
            """;
        await Verify<PrivateFieldNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_const_field()
    {
        var source = """
            class C {
                private const int MaxRetry = 3;
            }
            """;
        await Verify<PrivateFieldNamingAnalyzer>.AnalyzerAsync(source);
    }

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

    [Fact]
    public async Task CodeFix_lowercases_PascalCase_and_adds_underscore()
    {
        var source = """
            class C {
                private string {|MN001:Name|};
            }
            """;
        var fixedSource = """
            class C {
                private string _name;
            }
            """;
        await VerifyFix<PrivateFieldNamingAnalyzer, PrivateFieldNamingCodeFix>
            .CodeFixAsync(source, fixedSource);
    }
}
```

- [ ] **Step 2: Run test — expect failure**

```bash
dotnet test tests/MarketNest.Analyzers.Tests/ --filter "PrivateFieldNaming" --no-build 2>&1 | head -20
```

Expected: build error or test failure because `PrivateFieldNamingAnalyzer` does not exist yet.

- [ ] **Step 3: Implement `PrivateFieldNamingAnalyzer`**

```csharp
// src/MarketNest.Analyzers/Analyzers/Naming/PrivateFieldNamingAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Naming;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrivateFieldNamingAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN001,
        title: "Private field must use _camelCase",
        messageFormat: "Private field '{0}' must be named with an underscore prefix and camelCase (e.g. '_myField')",
        category: "Naming",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Private fields must follow the _camelCase convention (§2.2).");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        var modifiers = field.Modifiers;

        if (!modifiers.Any(SyntaxKind.PrivateKeyword)) return;
        if (modifiers.Any(SyntaxKind.ConstKeyword)) return;
        if (modifiers.Any(SyntaxKind.StaticKeyword) && modifiers.Any(SyntaxKind.ReadOnlyKeyword)) return;

        foreach (var variable in field.Declaration.Variables)
        {
            var name = variable.Identifier.Text;
            if (!IsValidName(name))
                context.ReportDiagnostic(Diagnostic.Create(Rule, variable.Identifier.GetLocation(), name));
        }
    }

    private static bool IsValidName(string name) =>
        name.Length >= 2 && name[0] == '_' && char.IsLower(name[1]);
}
```

- [ ] **Step 4: Implement `PrivateFieldNamingCodeFix`**

```csharp
// src/MarketNest.Analyzers/CodeFixes/PrivateFieldNamingCodeFix.cs
using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace MarketNest.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PrivateFieldNamingCodeFix)), Shared]
public sealed class PrivateFieldNamingCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(DiagnosticIds.MN001);

    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        if (root is null) return;

        var token = root.FindToken(context.Diagnostics[0].Location.SourceSpan.Start);
        if (token.Parent is not VariableDeclaratorSyntax declarator) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Rename to _camelCase",
                createChangedSolution: ct => RenameAsync(context.Document, declarator, ct),
                equivalenceKey: nameof(PrivateFieldNamingCodeFix)),
            context.Diagnostics[0]);
    }

    private static async Task<Solution> RenameAsync(
        Document document, VariableDeclaratorSyntax declarator, CancellationToken ct)
    {
        var semanticModel = await document.GetSemanticModelAsync(ct);
        if (semanticModel is null) return document.Project.Solution;

        if (semanticModel.GetDeclaredSymbol(declarator, ct) is not IFieldSymbol symbol)
            return document.Project.Solution;

        var newName = ToUnderscoreCamelCase(symbol.Name);
        return await Renamer.RenameSymbolAsync(
            document.Project.Solution, symbol, new SymbolRenameOptions(), newName, ct);
    }

    internal static string ToUnderscoreCamelCase(string name)
    {
        if (name.StartsWith("m_", StringComparison.Ordinal)) name = name[2..];
        name = name.TrimStart('_');
        if (name.Length == 0) return "_field";
        return "_" + char.ToLowerInvariant(name[0]) + name[1..];
    }
}
```

- [ ] **Step 5: Run tests — expect pass**

```bash
dotnet test tests/MarketNest.Analyzers.Tests/ --filter "PrivateFieldNaming"
```

Expected: `5 passed`.

- [ ] **Step 6: Commit**

```bash
git add src/MarketNest.Analyzers/Analyzers/Naming/PrivateFieldNamingAnalyzer.cs \
        src/MarketNest.Analyzers/CodeFixes/PrivateFieldNamingCodeFix.cs \
        tests/MarketNest.Analyzers.Tests/Naming/PrivateFieldNamingAnalyzerTests.cs
git commit -m "feat(analyzers): MN001 private field _camelCase naming with code fix"
```

---

## Task 3: MN002 — Banned class suffixes

**Files:**
- Create: `src/MarketNest.Analyzers/Analyzers/Naming/BannedClassSuffixAnalyzer.cs`
- Create: `tests/MarketNest.Analyzers.Tests/Naming/BannedClassSuffixAnalyzerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MarketNest.Analyzers.Tests/Naming/BannedClassSuffixAnalyzerTests.cs
using MarketNest.Analyzers.Naming;
using Xunit;

namespace MarketNest.Analyzers.Tests.Naming;

public class BannedClassSuffixAnalyzerTests
{
    [Theory]
    [InlineData("OrderManager")]
    [InlineData("CartHelper")]
    [InlineData("StringUtils")]
    public async Task Triggers_for_banned_suffix(string className)
    {
        var source = $$"""
            class {|MN002:{{className}}|} { }
            """;
        await Verify<BannedClassSuffixAnalyzer>.AnalyzerAsync(source);
    }

    [Theory]
    [InlineData("OrderRepository")]
    [InlineData("PlaceOrderCommand")]
    [InlineData("GetOrderDetailQuery")]
    public async Task No_trigger_for_valid_class_name(string className)
    {
        var source = $$"""
            class {{className}} { }
            """;
        await Verify<BannedClassSuffixAnalyzer>.AnalyzerAsync(source);
    }
}
```

- [ ] **Step 2: Implement `BannedClassSuffixAnalyzer`**

```csharp
// src/MarketNest.Analyzers/Analyzers/Naming/BannedClassSuffixAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Naming;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BannedClassSuffixAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] BannedSuffixes = ["Manager", "Helper", "Utils"];

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN002,
        title: "Banned class suffix",
        messageFormat: "Class '{0}' uses banned suffix '{1}'. Use a more descriptive name (§2.2).",
        category: "Naming",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var name = classDecl.Identifier.Text;

        foreach (var suffix in BannedSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule, classDecl.Identifier.GetLocation(), name, suffix));
                return;
            }
        }
    }
}
```

- [ ] **Step 3: Run tests and commit**

```bash
dotnet test tests/MarketNest.Analyzers.Tests/ --filter "BannedClassSuffix"
```

Expected: `5 passed`.

```bash
git add src/MarketNest.Analyzers/Analyzers/Naming/BannedClassSuffixAnalyzer.cs \
        tests/MarketNest.Analyzers.Tests/Naming/BannedClassSuffixAnalyzerTests.cs
git commit -m "feat(analyzers): MN002 banned class suffixes (Manager/Helper/Utils)"
```

---

## Task 4: MN003 — Async void + code fix

**Files:**
- Create: `src/MarketNest.Analyzers/Analyzers/AsyncRules/AsyncVoidAnalyzer.cs`
- Create: `src/MarketNest.Analyzers/CodeFixes/AsyncVoidCodeFix.cs`
- Create: `tests/MarketNest.Analyzers.Tests/AsyncRules/AsyncVoidAnalyzerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MarketNest.Analyzers.Tests/AsyncRules/AsyncVoidAnalyzerTests.cs
using MarketNest.Analyzers.AsyncRules;
using MarketNest.Analyzers.CodeFixes;
using Xunit;

namespace MarketNest.Analyzers.Tests.AsyncRules;

public class AsyncVoidAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_async_void_method()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                public async void {|MN003:HandleOrder|}() { await Task.Delay(1); }
            }
            """;
        await Verify<AsyncVoidAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_async_task()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                public async Task HandleOrder() { await Task.Delay(1); }
            }
            """;
        await Verify<AsyncVoidAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_event_handler_with_EventArgs()
    {
        var source = """
            using System;
            using System.Threading.Tasks;
            class C {
                public async void OnClick(object sender, EventArgs e) { await Task.Delay(1); }
            }
            """;
        await Verify<AsyncVoidAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task CodeFix_changes_void_to_Task()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                public async void {|MN003:HandleOrder|}() { await Task.Delay(1); }
            }
            """;
        var fixedSource = """
            using System.Threading.Tasks;
            class C {
                public async Task HandleOrder() { await Task.Delay(1); }
            }
            """;
        await VerifyFix<AsyncVoidAnalyzer, AsyncVoidCodeFix>.CodeFixAsync(source, fixedSource);
    }
}
```

- [ ] **Step 2: Implement `AsyncVoidAnalyzer`**

```csharp
// src/MarketNest.Analyzers/Analyzers/AsyncRules/AsyncVoidAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.AsyncRules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncVoidAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN003,
        title: "Async void method",
        messageFormat: "Method '{0}' must not be async void. Use async Task instead (§2.4).",
        category: "AsyncRules",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        if (!method.Modifiers.Any(SyntaxKind.AsyncKeyword)) return;
        if (method.ReturnType is not PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword })
            return;

        // Skip genuine event handlers: have a parameter whose type name ends with "EventArgs"
        foreach (var param in method.ParameterList.Parameters)
        {
            var typeName = param.Type?.ToString() ?? string.Empty;
            if (typeName.EndsWith("EventArgs", StringComparison.Ordinal)) return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, method.Identifier.GetLocation(), method.Identifier.Text));
    }
}
```

- [ ] **Step 3: Implement `AsyncVoidCodeFix`**

```csharp
// src/MarketNest.Analyzers/CodeFixes/AsyncVoidCodeFix.cs
using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MarketNest.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncVoidCodeFix)), Shared]
public sealed class AsyncVoidCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.MN003);
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        if (root is null) return;

        var node = root.FindNode(context.Diagnostics[0].Location.SourceSpan);
        var method = node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Change return type to Task",
                createChangedDocument: ct => ChangeToTaskAsync(context.Document, method, ct),
                equivalenceKey: nameof(AsyncVoidCodeFix)),
            context.Diagnostics[0]);
    }

    private static async Task<Document> ChangeToTaskAsync(
        Document document, MethodDeclarationSyntax method, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is null) return document;

        var taskType = SyntaxFactory.ParseTypeName("Task").WithTriviaFrom(method.ReturnType);
        var newMethod = method.WithReturnType(taskType);
        var newRoot = root.ReplaceNode(method, newMethod);
        return document.WithSyntaxRoot(newRoot);
    }
}
```

- [ ] **Step 4: Run tests and commit**

```bash
dotnet test tests/MarketNest.Analyzers.Tests/ --filter "AsyncVoid"
```

Expected: `4 passed`.

```bash
git add src/MarketNest.Analyzers/Analyzers/AsyncRules/AsyncVoidAnalyzer.cs \
        src/MarketNest.Analyzers/CodeFixes/AsyncVoidCodeFix.cs \
        tests/MarketNest.Analyzers.Tests/AsyncRules/AsyncVoidAnalyzerTests.cs
git commit -m "feat(analyzers): MN003 async void with Task code fix"
```

---

## Task 5: MN004 — Blocking async calls

**Files:**
- Create: `src/MarketNest.Analyzers/Analyzers/AsyncRules/BlockingAsyncAnalyzer.cs`
- Create: `tests/MarketNest.Analyzers.Tests/AsyncRules/BlockingAsyncAnalyzerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MarketNest.Analyzers.Tests/AsyncRules/BlockingAsyncAnalyzerTests.cs
using MarketNest.Analyzers.AsyncRules;
using Xunit;

namespace MarketNest.Analyzers.Tests.AsyncRules;

public class BlockingAsyncAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_dot_Result()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                void M() {
                    var t = Task.FromResult(1);
                    var x = {|MN004:t.Result|};
                }
            }
            """;
        await Verify<BlockingAsyncAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_GetAwaiter_GetResult()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                void M() {
                    var t = Task.FromResult(1);
                    var x = {|MN004:t.GetAwaiter()|}.GetResult();
                }
            }
            """;
        await Verify<BlockingAsyncAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_await()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                async Task M() {
                    var x = await Task.FromResult(1);
                }
            }
            """;
        await Verify<BlockingAsyncAnalyzer>.AnalyzerAsync(source);
    }
}
```

- [ ] **Step 2: Implement `BlockingAsyncAnalyzer`**

```csharp
// src/MarketNest.Analyzers/Analyzers/AsyncRules/BlockingAsyncAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.AsyncRules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BlockingAsyncAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN004,
        title: "Blocking async call",
        messageFormat: "Blocking on async code via '{0}' can cause deadlocks. Use await instead (§2.4).",
        category: "AsyncRules",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MemberAccessExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        var memberName = memberAccess.Name.Identifier.Text;

        if (memberName == "Result")
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression);
            if (IsTaskLike(typeInfo.Type))
                context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation(), ".Result"));
        }
        else if (memberName == "GetAwaiter")
        {
            // Detect .GetAwaiter().GetResult() chain
            if (memberAccess.Parent is not InvocationExpressionSyntax getAwaiterCall) return;
            if (getAwaiterCall.Parent is not MemberAccessExpressionSyntax getResultAccess) return;
            if (getResultAccess.Name.Identifier.Text != "GetResult") return;

            var typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression);
            if (IsTaskLike(typeInfo.Type))
                context.ReportDiagnostic(Diagnostic.Create(Rule, memberAccess.GetLocation(), ".GetAwaiter().GetResult()"));
        }
    }

    private static bool IsTaskLike(ITypeSymbol? type)
    {
        if (type is null) return false;
        var name = type.OriginalDefinition.ToDisplayString();
        return name.StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal)
            || name.StartsWith("System.Threading.Tasks.ValueTask", StringComparison.Ordinal);
    }
}
```

- [ ] **Step 3: Run tests and commit**

```bash
dotnet test tests/MarketNest.Analyzers.Tests/ --filter "BlockingAsync"
git add src/MarketNest.Analyzers/Analyzers/AsyncRules/BlockingAsyncAnalyzer.cs \
        tests/MarketNest.Analyzers.Tests/AsyncRules/BlockingAsyncAnalyzerTests.cs
git commit -m "feat(analyzers): MN004 blocking async calls (.Result / GetAwaiter)"
```

---

## Task 6: MN012–MN015 — Command / Query / Handler / Event naming

**Files:**
- Create: `src/MarketNest.Analyzers/Analyzers/Naming/CommandQueryNamingAnalyzer.cs`
- Create: `tests/MarketNest.Analyzers.Tests/Naming/CommandQueryNamingAnalyzerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MarketNest.Analyzers.Tests/Naming/CommandQueryNamingAnalyzerTests.cs
using MarketNest.Analyzers.Naming;
using Xunit;

namespace MarketNest.Analyzers.Tests.Naming;

public class CommandQueryNamingAnalyzerTests
{
    // These tests use inline interface stubs since the real project types aren't referenced.

    [Fact]
    public async Task MN012_triggers_when_ICommand_class_lacks_Command_suffix()
    {
        var source = """
            interface ICommand<T> { }
            class {|MN012:PlaceOrder|} : ICommand<string> { }
            """;
        await Verify<CommandQueryNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task MN012_no_trigger_when_ICommand_class_has_Command_suffix()
    {
        var source = """
            interface ICommand<T> { }
            class PlaceOrderCommand : ICommand<string> { }
            """;
        await Verify<CommandQueryNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task MN013_triggers_when_IQuery_class_lacks_Query_suffix()
    {
        var source = """
            interface IQuery<T> { }
            class {|MN013:GetOrderDetail|} : IQuery<string> { }
            """;
        await Verify<CommandQueryNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task MN013_no_trigger_when_IQuery_class_has_correct_naming()
    {
        var source = """
            interface IQuery<T> { }
            class GetOrderDetailQuery : IQuery<string> { }
            """;
        await Verify<CommandQueryNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task MN014_triggers_when_ICommandHandler_lacks_Handler_suffix()
    {
        var source = """
            using System.Threading.Tasks;
            interface ICommandHandler<TCommand, TResult> { Task Handle(TCommand c); }
            class {|MN014:PlaceOrderProcessor|} : ICommandHandler<string, int> {
                public Task Handle(string c) => Task.CompletedTask;
            }
            """;
        await Verify<CommandQueryNamingAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task MN015_triggers_when_IDomainEvent_record_lacks_Event_suffix()
    {
        var source = """
            interface IDomainEvent { }
            record {|MN015:OrderPlaced|} : IDomainEvent;
            """;
        await Verify<CommandQueryNamingAnalyzer>.AnalyzerAsync(source);
    }
}
```

- [ ] **Step 2: Implement `CommandQueryNamingAnalyzer`**

```csharp
// src/MarketNest.Analyzers/Analyzers/Naming/CommandQueryNamingAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Naming;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CommandQueryNamingAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor CommandRule = new(
        DiagnosticIds.MN012, "Command naming", "'{0}' implements ICommand but does not end with 'Command' (§2.2).",
        "Naming", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor QueryRule = new(
        DiagnosticIds.MN013, "Query naming", "'{0}' implements IQuery but does not start with 'Get' and end with 'Query' (§2.2).",
        "Naming", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor HandlerRule = new(
        DiagnosticIds.MN014, "Handler naming", "'{0}' implements ICommandHandler/IQueryHandler but does not end with 'Handler' (§2.2).",
        "Naming", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EventRule = new(
        DiagnosticIds.MN015, "Event naming", "'{0}' implements IDomainEvent/IIntegrationEvent but does not end with 'Event' (§2.2).",
        "Naming", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(CommandRule, QueryRule, HandlerRule, EventRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze,
            SyntaxKind.ClassDeclaration, SyntaxKind.RecordDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var typeDecl = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol symbol) return;

        var name = symbol.Name;
        foreach (var iface in symbol.AllInterfaces)
        {
            var ifaceName = iface.OriginalDefinition.Name;
            switch (ifaceName)
            {
                case "ICommand" when !name.EndsWith("Command", StringComparison.Ordinal):
                    context.ReportDiagnostic(Diagnostic.Create(CommandRule, typeDecl.Identifier.GetLocation(), name));
                    return;
                case "IQuery" when !name.StartsWith("Get", StringComparison.Ordinal) || !name.EndsWith("Query", StringComparison.Ordinal):
                    context.ReportDiagnostic(Diagnostic.Create(QueryRule, typeDecl.Identifier.GetLocation(), name));
                    return;
                case "ICommandHandler" or "IQueryHandler" when !name.EndsWith("Handler", StringComparison.Ordinal):
                    context.ReportDiagnostic(Diagnostic.Create(HandlerRule, typeDecl.Identifier.GetLocation(), name));
                    return;
                case "IDomainEvent" or "IIntegrationEvent" when !name.EndsWith("Event", StringComparison.Ordinal):
                    context.ReportDiagnostic(Diagnostic.Create(EventRule, typeDecl.Identifier.GetLocation(), name));
                    return;
            }
        }
    }
}
```

- [ ] **Step 3: Run tests and commit**

```bash
dotnet test tests/MarketNest.Analyzers.Tests/ --filter "CommandQueryNaming"
git add src/MarketNest.Analyzers/Analyzers/Naming/CommandQueryNamingAnalyzer.cs \
        tests/MarketNest.Analyzers.Tests/Naming/CommandQueryNamingAnalyzerTests.cs
git commit -m "feat(analyzers): MN012-MN015 Command/Query/Handler/Event naming conventions"
```

---

## Task 7: MN005 — Direct ILogger calls

**Files:**
- Create: `src/MarketNest.Analyzers/Analyzers/Logging/DirectLoggerCallAnalyzer.cs`
- Create: `tests/MarketNest.Analyzers.Tests/Logging/DirectLoggerCallAnalyzerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MarketNest.Analyzers.Tests/Logging/DirectLoggerCallAnalyzerTests.cs
using MarketNest.Analyzers.Logging;
using Xunit;

namespace MarketNest.Analyzers.Tests.Logging;

public class DirectLoggerCallAnalyzerTests
{
    [Theory]
    [InlineData("LogInformation")]
    [InlineData("LogWarning")]
    [InlineData("LogError")]
    [InlineData("LogDebug")]
    public async Task Triggers_for_direct_logger_extension_call(string methodName)
    {
        var source = $$"""
            using Microsoft.Extensions.Logging;
            class C {
                private readonly ILogger<C> _logger;
                void M() { {|MN005:_logger.{{methodName}}("msg")|};  }
            }
            """;
        await Verify<DirectLoggerCallAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_Log_nested_class_call()
    {
        var source = """
            using Microsoft.Extensions.Logging;
            partial class C {
                private readonly ILogger<C> _logger;
                void M() { Log.InfoStart(_logger, "x"); }
                private static partial class Log {
                    public static void InfoStart(ILogger logger, string s) { }
                }
            }
            """;
        await Verify<DirectLoggerCallAnalyzer>.AnalyzerAsync(source);
    }
}
```

- [ ] **Step 2: Implement `DirectLoggerCallAnalyzer`**

```csharp
// src/MarketNest.Analyzers/Analyzers/Logging/DirectLoggerCallAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Logging;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DirectLoggerCallAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> LogMethods = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "LogTrace", "LogDebug", "LogInformation", "LogWarning", "LogError", "LogCritical");

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN005,
        title: "Direct ILogger call",
        messageFormat: "Do not call '{0}' directly on ILogger. Use [LoggerMessage] source-generated delegates via a nested 'Log' class (§9, ADR-014).",
        category: "Logging",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return;

        var methodName = memberAccess.Name.Identifier.Text;
        if (!LogMethods.Contains(methodName)) return;

        var typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression);
        var typeName = typeInfo.Type?.OriginalDefinition?.ToDisplayString() ?? string.Empty;

        if (!typeName.Contains("ILogger")) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), methodName));
    }
}
```

- [ ] **Step 3: Run tests and commit**

```bash
dotnet test tests/MarketNest.Analyzers.Tests/ --filter "DirectLoggerCall"
git add src/MarketNest.Analyzers/Analyzers/Logging/DirectLoggerCallAnalyzer.cs \
        tests/MarketNest.Analyzers.Tests/Logging/DirectLoggerCallAnalyzerTests.cs
git commit -m "feat(analyzers): MN005 ban direct ILogger.LogXxx calls"
```

---

## Task 8: MN006 — Logging class must be partial + code fix

**Files:**
- Create: `src/MarketNest.Analyzers/Analyzers/Logging/LoggingClassPartialAnalyzer.cs`
- Create: `src/MarketNest.Analyzers/CodeFixes/LoggingClassPartialCodeFix.cs`
- Create: `tests/MarketNest.Analyzers.Tests/Logging/LoggingClassPartialAnalyzerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MarketNest.Analyzers.Tests/Logging/LoggingClassPartialAnalyzerTests.cs
using MarketNest.Analyzers.CodeFixes;
using MarketNest.Analyzers.Logging;
using Xunit;

namespace MarketNest.Analyzers.Tests.Logging;

public class LoggingClassPartialAnalyzerTests
{
    [Fact]
    public async Task Triggers_when_class_has_IAppLogger_but_is_not_partial()
    {
        var source = """
            interface IAppLogger<T> { }
            class {|MN006:OrderHandler|}(IAppLogger<OrderHandler> _logger) { }
            """;
        await Verify<LoggingClassPartialAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_when_class_is_already_partial()
    {
        var source = """
            interface IAppLogger<T> { }
            partial class OrderHandler(IAppLogger<OrderHandler> _logger) { }
            """;
        await Verify<LoggingClassPartialAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_when_class_has_no_logger()
    {
        var source = """
            class OrderHandler(string name) { }
            """;
        await Verify<LoggingClassPartialAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task CodeFix_adds_partial_keyword()
    {
        var source = """
            interface IAppLogger<T> { }
            class {|MN006:OrderHandler|}(IAppLogger<OrderHandler> _logger) { }
            """;
        var fixedSource = """
            interface IAppLogger<T> { }
            partial class OrderHandler(IAppLogger<OrderHandler> _logger) { }
            """;
        await VerifyFix<LoggingClassPartialAnalyzer, LoggingClassPartialCodeFix>
            .CodeFixAsync(source, fixedSource);
    }
}
```

- [ ] **Step 2: Implement `LoggingClassPartialAnalyzer`**

```csharp
// src/MarketNest.Analyzers/Analyzers/Logging/LoggingClassPartialAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Logging;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LoggingClassPartialAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN006,
        title: "Logging class must be partial",
        messageFormat: "Class '{0}' injects IAppLogger<T> but is not declared as 'partial'. Add 'partial' to enable [LoggerMessage] source generation (§9.2).",
        category: "Logging",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        if (classDecl.Modifiers.Any(SyntaxKind.PartialKeyword)) return;
        if (!HasAppLogger(classDecl, context.SemanticModel)) return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule, classDecl.Identifier.GetLocation(), classDecl.Identifier.Text));
    }

    private static bool HasAppLogger(ClassDeclarationSyntax classDecl, SemanticModel model)
    {
        // Check primary constructor parameters
        if (classDecl.ParameterList is not null)
        {
            foreach (var param in classDecl.ParameterList.Parameters)
            {
                if (param.Type is null) continue;
                var typeInfo = model.GetTypeInfo(param.Type);
                if (IsAppLogger(typeInfo.Type)) return true;
            }
        }

        // Check field declarations
        foreach (var field in classDecl.Members.OfType<FieldDeclarationSyntax>())
        {
            var typeInfo = model.GetTypeInfo(field.Declaration.Type);
            if (IsAppLogger(typeInfo.Type)) return true;
        }
        return false;
    }

    internal static bool IsAppLogger(ITypeSymbol? type) =>
        type?.OriginalDefinition?.ToDisplayString()
            .Contains("IAppLogger", StringComparison.Ordinal) == true;
}
```

- [ ] **Step 3: Implement `LoggingClassPartialCodeFix`**

```csharp
// src/MarketNest.Analyzers/CodeFixes/LoggingClassPartialCodeFix.cs
using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MarketNest.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LoggingClassPartialCodeFix)), Shared]
public sealed class LoggingClassPartialCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.MN006);
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        if (root is null) return;

        var node = root.FindNode(context.Diagnostics[0].Location.SourceSpan);
        var classDecl = node.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add 'partial' modifier",
                createChangedDocument: ct => AddPartialAsync(context.Document, classDecl, ct),
                equivalenceKey: nameof(LoggingClassPartialCodeFix)),
            context.Diagnostics[0]);
    }

    private static async Task<Document> AddPartialAsync(
        Document document, ClassDeclarationSyntax classDecl, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is null) return document;

        var partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword)
            .WithTrailingTrivia(SyntaxFactory.Space);
        var newModifiers = classDecl.Modifiers.Add(partialToken);
        var newClass = classDecl.WithModifiers(newModifiers);
        return document.WithSyntaxRoot(root.ReplaceNode(classDecl, newClass));
    }
}
```

- [ ] **Step 4: Run tests and commit**

```bash
dotnet test tests/MarketNest.Analyzers.Tests/ --filter "LoggingClassPartial"
git add src/MarketNest.Analyzers/Analyzers/Logging/LoggingClassPartialAnalyzer.cs \
        src/MarketNest.Analyzers/CodeFixes/LoggingClassPartialCodeFix.cs \
        tests/MarketNest.Analyzers.Tests/Logging/LoggingClassPartialAnalyzerTests.cs
git commit -m "feat(analyzers): MN006 logging class must be partial with code fix"
```

---

## Task 9: MN007 — Inject IAppLogger not ILogger + code fix

**Files:**
- Create: `src/MarketNest.Analyzers/Analyzers/Logging/AppLoggerInjectionAnalyzer.cs`
- Create: `src/MarketNest.Analyzers/CodeFixes/AppLoggerInjectionCodeFix.cs`
- Create: `tests/MarketNest.Analyzers.Tests/Logging/AppLoggerInjectionAnalyzerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MarketNest.Analyzers.Tests/Logging/AppLoggerInjectionAnalyzerTests.cs
using MarketNest.Analyzers.CodeFixes;
using MarketNest.Analyzers.Logging;
using Xunit;

namespace MarketNest.Analyzers.Tests.Logging;

public class AppLoggerInjectionAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_ILogger_in_primary_constructor()
    {
        var source = """
            using Microsoft.Extensions.Logging;
            partial class Handler(
                {|MN007:ILogger<Handler>|} _logger) { }
            """;
        await Verify<AppLoggerInjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_IAppLogger()
    {
        var source = """
            interface IAppLogger<T> { }
            partial class Handler(IAppLogger<Handler> _logger) { }
            """;
        await Verify<AppLoggerInjectionAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task CodeFix_replaces_ILogger_with_IAppLogger()
    {
        var source = """
            using Microsoft.Extensions.Logging;
            partial class Handler(
                {|MN007:ILogger<Handler>|} _logger) { }
            """;
        var fixedSource = """
            using Microsoft.Extensions.Logging;
            using MarketNest.Base.Infrastructure;
            partial class Handler(
                IAppLogger<Handler> _logger) { }
            """;
        await VerifyFix<AppLoggerInjectionAnalyzer, AppLoggerInjectionCodeFix>
            .CodeFixAsync(source, fixedSource);
    }
}
```

- [ ] **Step 2: Implement `AppLoggerInjectionAnalyzer`**

```csharp
// src/MarketNest.Analyzers/Analyzers/Logging/AppLoggerInjectionAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Logging;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AppLoggerInjectionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN007,
        title: "Must inject IAppLogger<T> not ILogger<T>",
        messageFormat: "Inject 'IAppLogger<T>' instead of '{0}'. IAppLogger wraps ILogger and is required by the project logging standard (§9.2).",
        category: "Logging",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.Parameter);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var param = (ParameterSyntax)context.Node;
        if (param.Type is null) return;

        var typeInfo = context.SemanticModel.GetTypeInfo(param.Type);
        var typeName = typeInfo.Type?.OriginalDefinition?.ToDisplayString() ?? string.Empty;

        if (typeName.StartsWith("Microsoft.Extensions.Logging.ILogger<", StringComparison.Ordinal))
            context.ReportDiagnostic(Diagnostic.Create(Rule, param.Type.GetLocation(), typeName));
    }
}
```

- [ ] **Step 3: Implement `AppLoggerInjectionCodeFix`**

```csharp
// src/MarketNest.Analyzers/CodeFixes/AppLoggerInjectionCodeFix.cs
using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MarketNest.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AppLoggerInjectionCodeFix)), Shared]
public sealed class AppLoggerInjectionCodeFix : CodeFixProvider
{
    private const string AppLoggerNamespace = "MarketNest.Base.Infrastructure";

    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.MN007);
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        if (root is null) return;

        var node = root.FindNode(context.Diagnostics[0].Location.SourceSpan);
        if (node is not GenericNameSyntax genericName) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Replace with IAppLogger<T>",
                createChangedDocument: ct => ReplaceAsync(context.Document, genericName, ct),
                equivalenceKey: nameof(AppLoggerInjectionCodeFix)),
            context.Diagnostics[0]);
    }

    private static async Task<Document> ReplaceAsync(
        Document document, GenericNameSyntax oldType, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is null) return document;

        var typeArg = oldType.TypeArgumentList.Arguments.FirstOrDefault();
        if (typeArg is null) return document;

        var newTypeName = SyntaxFactory.GenericName(
            SyntaxFactory.Identifier("IAppLogger"),
            SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(typeArg)))
            .WithTriviaFrom(oldType);

        var newRoot = root.ReplaceNode(oldType, newTypeName);
        newRoot = AddUsingIfMissing((CompilationUnitSyntax)newRoot, AppLoggerNamespace);
        return document.WithSyntaxRoot(newRoot);
    }

    private static CompilationUnitSyntax AddUsingIfMissing(CompilationUnitSyntax root, string ns)
    {
        if (root.Usings.Any(u => u.Name?.ToString() == ns)) return root;
        var directive = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ns))
            .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
        return root.AddUsings(directive);
    }
}
```

- [ ] **Step 4: Run tests and commit**

```bash
dotnet test tests/MarketNest.Analyzers.Tests/ --filter "AppLoggerInjection"
git add src/MarketNest.Analyzers/Analyzers/Logging/AppLoggerInjectionAnalyzer.cs \
        src/MarketNest.Analyzers/CodeFixes/AppLoggerInjectionCodeFix.cs \
        tests/MarketNest.Analyzers.Tests/Logging/AppLoggerInjectionAnalyzerTests.cs
git commit -m "feat(analyzers): MN007 ILogger<T> → IAppLogger<T> injection with code fix"
```

---

## Task 10: MN017 — Unnecessary Task.FromResult + code fix

**Files:**
- Create: `src/MarketNest.Analyzers/Analyzers/AsyncRules/TaskFromResultAnalyzer.cs`
- Create: `src/MarketNest.Analyzers/CodeFixes/TaskFromResultCodeFix.cs`
- Create: `tests/MarketNest.Analyzers.Tests/AsyncRules/TaskFromResultAnalyzerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MarketNest.Analyzers.Tests/AsyncRules/TaskFromResultAnalyzerTests.cs
using MarketNest.Analyzers.AsyncRules;
using MarketNest.Analyzers.CodeFixes;
using Xunit;

namespace MarketNest.Analyzers.Tests.AsyncRules;

public class TaskFromResultAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_await_Task_FromResult()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                async Task<int> M() => {|MN017:await Task.FromResult(42)|};
            }
            """;
        await Verify<TaskFromResultAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_await_on_real_async()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                async Task<int> M() => await Task.Delay(1).ContinueWith(_ => 42);
            }
            """;
        await Verify<TaskFromResultAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task CodeFix_removes_await_and_unwraps_value()
    {
        var source = """
            using System.Threading.Tasks;
            class C {
                async Task<int> M() => {|MN017:await Task.FromResult(42)|};
            }
            """;
        var fixedSource = """
            using System.Threading.Tasks;
            class C {
                async Task<int> M() => 42;
            }
            """;
        await VerifyFix<TaskFromResultAnalyzer, TaskFromResultCodeFix>.CodeFixAsync(source, fixedSource);
    }
}
```

- [ ] **Step 2: Implement `TaskFromResultAnalyzer`**

```csharp
// src/MarketNest.Analyzers/Analyzers/AsyncRules/TaskFromResultAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.AsyncRules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TaskFromResultAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN017,
        title: "Unnecessary Task.FromResult",
        messageFormat: "'await Task.FromResult(...)' is redundant. Return the value directly (§2.4).",
        category: "AsyncRules",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.AwaitExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var awaitExpr = (AwaitExpressionSyntax)context.Node;
        if (awaitExpr.Expression is not InvocationExpressionSyntax invocation) return;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return;
        if (memberAccess.Name.Identifier.Text != "FromResult") return;

        var typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression);
        var typeName = typeInfo.Type?.ToDisplayString() ?? string.Empty;
        if (!typeName.Contains("Task")) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, awaitExpr.GetLocation()));
    }
}
```

- [ ] **Step 3: Implement `TaskFromResultCodeFix`**

```csharp
// src/MarketNest.Analyzers/CodeFixes/TaskFromResultCodeFix.cs
using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MarketNest.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TaskFromResultCodeFix)), Shared]
public sealed class TaskFromResultCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.MN017);
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        if (root is null) return;

        var awaitExpr = root.FindNode(context.Diagnostics[0].Location.SourceSpan)
            as AwaitExpressionSyntax;
        if (awaitExpr is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Remove await Task.FromResult(…) wrapper",
                createChangedDocument: ct => UnwrapAsync(context.Document, awaitExpr, ct),
                equivalenceKey: nameof(TaskFromResultCodeFix)),
            context.Diagnostics[0]);
    }

    private static async Task<Document> UnwrapAsync(
        Document document, AwaitExpressionSyntax awaitExpr, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is null) return document;

        var invocation = (InvocationExpressionSyntax)awaitExpr.Expression;
        var argument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        if (argument is null) return document;

        var replacement = argument.WithTriviaFrom(awaitExpr);
        return document.WithSyntaxRoot(root.ReplaceNode(awaitExpr, replacement));
    }
}
```

- [ ] **Step 4: Run tests and commit**

```bash
dotnet test tests/MarketNest.Analyzers.Tests/ --filter "TaskFromResult"
git add src/MarketNest.Analyzers/Analyzers/AsyncRules/TaskFromResultAnalyzer.cs \
        src/MarketNest.Analyzers/CodeFixes/TaskFromResultCodeFix.cs \
        tests/MarketNest.Analyzers.Tests/AsyncRules/TaskFromResultAnalyzerTests.cs
git commit -m "feat(analyzers): MN017 unnecessary Task.FromResult with code fix"
```

---

## Task 11: MN011 — Public async API missing CancellationToken

**Files:**
- Create: `src/MarketNest.Analyzers/Analyzers/AsyncRules/CancellationTokenAnalyzer.cs`
- Create: `tests/MarketNest.Analyzers.Tests/AsyncRules/CancellationTokenAnalyzerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MarketNest.Analyzers.Tests/AsyncRules/CancellationTokenAnalyzerTests.cs
using MarketNest.Analyzers.AsyncRules;
using Xunit;

namespace MarketNest.Analyzers.Tests.AsyncRules;

public class CancellationTokenAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_interface_async_method_missing_CT()
    {
        var source = """
            using System.Threading.Tasks;
            interface IOrderRepository {
                Task<string?> {|MN011:GetByIdAsync|}(int id);
            }
            """;
        await Verify<CancellationTokenAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_abstract_method_missing_CT()
    {
        var source = """
            using System.Threading.Tasks;
            abstract class Base {
                public abstract Task {|MN011:SaveAsync|}();
            }
            """;
        await Verify<CancellationTokenAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_when_CT_present()
    {
        var source = """
            using System.Threading;
            using System.Threading.Tasks;
            interface IOrderRepository {
                Task<string?> GetByIdAsync(int id, CancellationToken ct);
            }
            """;
        await Verify<CancellationTokenAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_concrete_implementation()
    {
        var source = """
            using System.Threading.Tasks;
            class Repo {
                public Task<string?> GetByIdAsync(int id) => Task.FromResult<string?>(null);
            }
            """;
        await Verify<CancellationTokenAnalyzer>.AnalyzerAsync(source);
    }
}
```

- [ ] **Step 2: Implement `CancellationTokenAnalyzer`**

```csharp
// src/MarketNest.Analyzers/Analyzers/AsyncRules/CancellationTokenAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.AsyncRules;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CancellationTokenAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN011,
        title: "Public async API missing CancellationToken",
        messageFormat: "Method '{0}' is async/Task-returning but has no CancellationToken parameter. Add 'CancellationToken ct' as the last parameter (§2.9).",
        category: "AsyncRules",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        // Only interface members and abstract methods
        var parent = method.Parent;
        var isInterfaceMember = parent is InterfaceDeclarationSyntax;
        var isAbstract = method.Modifiers.Any(SyntaxKind.AbstractKeyword);
        if (!isInterfaceMember && !isAbstract) return;

        // Must be async or return Task/ValueTask
        var isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);
        if (!isAsync && !ReturnsTaskLike(method.ReturnType)) return;

        // Already has CancellationToken
        foreach (var param in method.ParameterList.Parameters)
        {
            if (param.Type?.ToString().Contains("CancellationToken") == true) return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, method.Identifier.GetLocation(), method.Identifier.Text));
    }

    private static bool ReturnsTaskLike(TypeSyntax t) =>
        t.ToString() is var s && (s.StartsWith("Task", StringComparison.Ordinal) || s.StartsWith("ValueTask", StringComparison.Ordinal));
}
```

- [ ] **Step 3: Run tests and commit**

```bash
dotnet test tests/MarketNest.Analyzers.Tests/ --filter "CancellationToken"
git add src/MarketNest.Analyzers/Analyzers/AsyncRules/CancellationTokenAnalyzer.cs \
        tests/MarketNest.Analyzers.Tests/AsyncRules/CancellationTokenAnalyzerTests.cs
git commit -m "feat(analyzers): MN011 public async API missing CancellationToken"
```

---

## Task 12: MN008 — Flat namespace (no sub-folder levels)

**Files:**
- Create: `src/MarketNest.Analyzers/Analyzers/Architecture/FlatNamespaceAnalyzer.cs`
- Create: `tests/MarketNest.Analyzers.Tests/Architecture/FlatNamespaceAnalyzerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MarketNest.Analyzers.Tests/Architecture/FlatNamespaceAnalyzerTests.cs
using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class FlatNamespaceAnalyzerTests
{
    [Theory]
    [InlineData("MarketNest.Orders.Application.Commands")]
    [InlineData("MarketNest.Identity.Domain.Entities")]
    [InlineData("MarketNest.Admin.Infrastructure.Persistence")]
    public async Task Triggers_when_namespace_has_more_than_three_segments(string ns)
    {
        var source = $$"""
            {|MN008:namespace {{ns}};|}
            class C { }
            """;
        await Verify<FlatNamespaceAnalyzer>.AnalyzerAsync(source);
    }

    [Theory]
    [InlineData("MarketNest.Orders.Application")]
    [InlineData("MarketNest.Identity.Domain")]
    [InlineData("MarketNest.Admin")]
    public async Task No_trigger_for_three_or_fewer_segments(string ns)
    {
        var source = $$"""
            namespace {{ns}};
            class C { }
            """;
        await Verify<FlatNamespaceAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_non_marketnest_namespace()
    {
        var source = """
            namespace Some.Other.Deep.Namespace;
            class C { }
            """;
        await Verify<FlatNamespaceAnalyzer>.AnalyzerAsync(source);
    }
}
```

- [ ] **Step 2: Implement `FlatNamespaceAnalyzer`**

```csharp
// src/MarketNest.Analyzers/Analyzers/Architecture/FlatNamespaceAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FlatNamespaceAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN008,
        title: "Namespace must be flat at layer level",
        messageFormat: "Namespace '{0}' exceeds three segments. Namespaces must stop at 'MarketNest.<Module>.<Layer>' — do not include sub-folder names (§2.7).",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze,
            SyntaxKind.NamespaceDeclaration,
            SyntaxKind.FileScopedNamespaceDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var name = context.Node switch
        {
            NamespaceDeclarationSyntax n => n.Name.ToString(),
            FileScopedNamespaceDeclarationSyntax f => f.Name.ToString(),
            _ => null
        };

        if (name is null) return;
        if (!name.StartsWith("MarketNest.", StringComparison.Ordinal)) return;
        if (name.Split('.').Length > 3)
            context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation(), name));
    }
}
```

- [ ] **Step 3: Run tests and commit**

```bash
dotnet test tests/MarketNest.Analyzers.Tests/ --filter "FlatNamespace"
git add src/MarketNest.Analyzers/Analyzers/Architecture/FlatNamespaceAnalyzer.cs \
        tests/MarketNest.Analyzers.Tests/Architecture/FlatNamespaceAnalyzerTests.cs
git commit -m "feat(analyzers): MN008 flat layer-level namespace enforcement"
```

---

## Task 13: MN009 — DateTime must be DateTimeOffset

**Files:**
- Create: `src/MarketNest.Analyzers/Analyzers/Architecture/DateTimeUsageAnalyzer.cs`
- Create: `tests/MarketNest.Analyzers.Tests/Architecture/DateTimeUsageAnalyzerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MarketNest.Analyzers.Tests/Architecture/DateTimeUsageAnalyzerTests.cs
using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class DateTimeUsageAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_DateTime_property()
    {
        var source = """
            using System;
            class Order {
                public {|MN009:DateTime|} CreatedAt { get; private set; }
            }
            """;
        await Verify<DateTimeUsageAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_nullable_DateTime_property()
    {
        var source = """
            using System;
            class Order {
                public {|MN009:DateTime?|} ShippedAt { get; private set; }
            }
            """;
        await Verify<DateTimeUsageAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_DateTimeOffset()
    {
        var source = """
            using System;
            class Order {
                public DateTimeOffset CreatedAt { get; private set; }
            }
            """;
        await Verify<DateTimeUsageAnalyzer>.AnalyzerAsync(source);
    }
}
```

- [ ] **Step 2: Implement `DateTimeUsageAnalyzer`**

```csharp
// src/MarketNest.Analyzers/Analyzers/Architecture/DateTimeUsageAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DateTimeUsageAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN009,
        title: "Use DateTimeOffset instead of DateTime",
        messageFormat: "Use 'DateTimeOffset' instead of 'DateTime' to preserve timezone information (§2.8).",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.FieldDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        TypeSyntax? typeSyntax = context.Node switch
        {
            PropertyDeclarationSyntax p => p.Type,
            FieldDeclarationSyntax f => f.Declaration.Type,
            _ => null
        };
        if (typeSyntax is null) return;

        var typeInfo = context.SemanticModel.GetTypeInfo(typeSyntax);
        if (IsDateTime(typeInfo.Type))
            context.ReportDiagnostic(Diagnostic.Create(Rule, typeSyntax.GetLocation()));
    }

    private static bool IsDateTime(ITypeSymbol? type)
    {
        if (type is null) return false;
        if (type.SpecialType == SpecialType.System_DateTime) return true;
        // Nullable<DateTime>
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } named)
            return named.TypeArguments.FirstOrDefault()?.SpecialType == SpecialType.System_DateTime;
        return false;
    }
}
```

- [ ] **Step 3: Run tests and commit**

```bash
dotnet test tests/MarketNest.Analyzers.Tests/ --filter "DateTimeUsage"
git add src/MarketNest.Analyzers/Analyzers/Architecture/DateTimeUsageAnalyzer.cs \
        tests/MarketNest.Analyzers.Tests/Architecture/DateTimeUsageAnalyzerTests.cs
git commit -m "feat(analyzers): MN009 DateTime must be DateTimeOffset"
```

---

## Task 14: MN010 — Service locator anti-pattern

**Files:**
- Create: `src/MarketNest.Analyzers/Analyzers/Architecture/ServiceLocatorAnalyzer.cs`
- Create: `tests/MarketNest.Analyzers.Tests/Architecture/ServiceLocatorAnalyzerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MarketNest.Analyzers.Tests/Architecture/ServiceLocatorAnalyzerTests.cs
using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class ServiceLocatorAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_GetRequiredService_in_CommandHandler()
    {
        var source = """
            using System;
            using System.Threading.Tasks;
            interface ICommandHandler<T, R> { Task Handle(T t); }
            interface IServiceProvider { object? GetService(Type t); }
            static class ServiceProviderExtensions {
                public static T GetRequiredService<T>(this IServiceProvider sp) => default!;
            }
            class PlaceOrderCommandHandler(IServiceProvider _sp) : ICommandHandler<string, int> {
                public Task Handle(string t) {
                    var svc = {|MN010:_sp.GetRequiredService<string>()|};
                    return Task.CompletedTask;
                }
            }
            """;
        await Verify<ServiceLocatorAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_outside_handler()
    {
        var source = """
            using System;
            interface IServiceProvider { object? GetService(Type t); }
            static class Ext { public static T GetRequiredService<T>(this IServiceProvider sp) => default!; }
            class Startup(IServiceProvider _sp) {
                void Configure() { var s = _sp.GetRequiredService<string>(); }
            }
            """;
        await Verify<ServiceLocatorAnalyzer>.AnalyzerAsync(source);
    }
}
```

- [ ] **Step 2: Implement `ServiceLocatorAnalyzer`**

```csharp
// src/MarketNest.Analyzers/Analyzers/Architecture/ServiceLocatorAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ServiceLocatorAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> ServiceLocatorMethods =
        ImmutableHashSet.Create(StringComparer.Ordinal, "GetService", "GetRequiredService");

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN010,
        title: "Service locator anti-pattern",
        messageFormat: "Do not use '{0}' inside handlers or PageModels. Declare dependencies in the constructor instead (§2.5).",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return;

        var methodName = memberAccess.Name.Identifier.Text;
        if (!ServiceLocatorMethods.Contains(methodName)) return;

        var containingClass = invocation.Ancestors()
            .OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass is null) return;

        if (context.SemanticModel.GetDeclaredSymbol(containingClass) is not INamedTypeSymbol classSymbol)
            return;

        if (IsHandlerOrPageModel(classSymbol))
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), methodName));
    }

    private static bool IsHandlerOrPageModel(INamedTypeSymbol symbol)
    {
        // Check base type chain for PageModel
        for (var t = symbol.BaseType; t is not null; t = t.BaseType)
            if (t.Name == "PageModel") return true;

        // Check interfaces for ICommandHandler / IQueryHandler
        foreach (var iface in symbol.AllInterfaces)
        {
            var n = iface.OriginalDefinition.Name;
            if (n is "ICommandHandler" or "IQueryHandler") return true;
        }
        return false;
    }
}
```

- [ ] **Step 3: Run tests and commit**

```bash
dotnet test tests/MarketNest.Analyzers.Tests/ --filter "ServiceLocator"
git add src/MarketNest.Analyzers/Analyzers/Architecture/ServiceLocatorAnalyzer.cs \
        tests/MarketNest.Analyzers.Tests/Architecture/ServiceLocatorAnalyzerTests.cs
git commit -m "feat(analyzers): MN010 service locator anti-pattern in handlers"
```

---

## Task 15: MN016 — Entity/Aggregate property must not have public setter

**Files:**
- Create: `src/MarketNest.Analyzers/Analyzers/Architecture/EntityPublicSetterAnalyzer.cs`
- Create: `tests/MarketNest.Analyzers.Tests/Architecture/EntityPublicSetterAnalyzerTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// tests/MarketNest.Analyzers.Tests/Architecture/EntityPublicSetterAnalyzerTests.cs
using MarketNest.Analyzers.Architecture;
using Xunit;

namespace MarketNest.Analyzers.Tests.Architecture;

public class EntityPublicSetterAnalyzerTests
{
    [Fact]
    public async Task Triggers_for_public_setter_on_Entity_subclass()
    {
        var source = """
            abstract class Entity<T> { }
            class Order : Entity<int> {
                public string {|MN016:Status|}  { get; set; } = "";
            }
            """;
        await Verify<EntityPublicSetterAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task Triggers_for_public_setter_on_AggregateRoot_subclass()
    {
        var source = """
            abstract class AggregateRoot { }
            class Order : AggregateRoot {
                public decimal {|MN016:Total|} { get; set; }
            }
            """;
        await Verify<EntityPublicSetterAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_private_setter()
    {
        var source = """
            abstract class Entity<T> { }
            class Order : Entity<int> {
                public string Status { get; private set; } = "";
            }
            """;
        await Verify<EntityPublicSetterAnalyzer>.AnalyzerAsync(source);
    }

    [Fact]
    public async Task No_trigger_for_non_entity_class()
    {
        var source = """
            class OrderDto {
                public string Status { get; set; } = "";
            }
            """;
        await Verify<EntityPublicSetterAnalyzer>.AnalyzerAsync(source);
    }
}
```

- [ ] **Step 2: Implement `EntityPublicSetterAnalyzer`**

```csharp
// src/MarketNest.Analyzers/Analyzers/Architecture/EntityPublicSetterAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EntityPublicSetterAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN016,
        title: "Entity/Aggregate property must not have public setter",
        messageFormat: "Property '{0}' on Entity/AggregateRoot has a public setter. Use '{{ get; private set; }}' and mutate via domain methods (ADR-007, §3.1).",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        var setter = property.AccessorList?.Accessors
            .FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        if (setter is null) return;

        // Setter is public if it has no accessibility modifier (inherits from property)
        // and the property itself is public
        var setterHasNoModifier = !setter.Modifiers.Any();
        var propertyIsPublic = property.Modifiers.Any(SyntaxKind.PublicKeyword);
        if (!(setterHasNoModifier && propertyIsPublic)) return;

        var containingClass = property.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass is null) return;

        if (context.SemanticModel.GetDeclaredSymbol(containingClass) is not INamedTypeSymbol classSymbol)
            return;

        if (InheritsFromEntityOrAggregate(classSymbol))
            context.ReportDiagnostic(Diagnostic.Create(
                Rule, setter.GetLocation(), property.Identifier.Text));
    }

    private static bool InheritsFromEntityOrAggregate(INamedTypeSymbol symbol)
    {
        for (var t = symbol.BaseType; t is not null; t = t.BaseType)
        {
            var name = t.OriginalDefinition.Name;
            if (name is "Entity" or "AggregateRoot") return true;
        }
        return false;
    }
}
```

- [ ] **Step 3: Run tests and commit**

```bash
dotnet test tests/MarketNest.Analyzers.Tests/ --filter "EntityPublicSetter"
git add src/MarketNest.Analyzers/Analyzers/Architecture/EntityPublicSetterAnalyzer.cs \
        tests/MarketNest.Analyzers.Tests/Architecture/EntityPublicSetterAnalyzerTests.cs
git commit -m "feat(analyzers): MN016 entity/aggregate property must not have public setter"
```

---

## Task 16: Run full test suite and wire analyzer to all src/ projects

**Files:**
- Create: `src/Directory.Build.targets`

- [ ] **Step 1: Run the full analyzer test suite**

```bash
dotnet test tests/MarketNest.Analyzers.Tests/
```

Expected: all tests pass (0 failures). Fix any failures before proceeding.

- [ ] **Step 2: Create `src/Directory.Build.targets`**

This file auto-wires the analyzer to every project under `src/`, excluding the analyzer project itself. The `Condition` prevents a circular reference.

```xml
<Project>
  <ItemGroup Condition="'$(MSBuildProjectName)' != 'MarketNest.Analyzers'">
    <ProjectReference Include="$(MSBuildThisFileDirectory)MarketNest.Analyzers/MarketNest.Analyzers.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Build the full solution**

```bash
dotnet build MarketNest.slnx
```

If any existing code violates an analyzer rule, the build will fail. Address violations one module at a time:

```bash
# Build one module at a time to isolate violations
dotnet build src/MarketNest.Core/MarketNest.Core.csproj
dotnet build src/Base/MarketNest.Base.Common/MarketNest.Base.Common.csproj
# ...etc
```

For violations that are intentional exceptions (e.g., generated code, infrastructure interfaces), suppress with:
```csharp
#pragma warning disable MN009 // DateTime in infrastructure model — intentional
public DateTime CreatedAt { get; set; }
#pragma warning restore MN009
```

- [ ] **Step 4: Run the full solution test suite**

```bash
dotnet test
```

Expected: all existing tests still pass.

- [ ] **Step 5: Final commit**

```bash
git add src/Directory.Build.targets
git commit -m "feat(analyzers): wire MarketNest.Analyzers to all src/ projects via Directory.Build.targets"
```
