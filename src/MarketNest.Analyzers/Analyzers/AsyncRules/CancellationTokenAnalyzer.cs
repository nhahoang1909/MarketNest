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
        messageFormat: "Method '{0}' is async/Task-returning but has no CancellationToken parameter — add 'CancellationToken ct' as the last parameter",
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
        var isInterfaceMember = method.Parent is InterfaceDeclarationSyntax;
        var isAbstract = method.Modifiers.Any(SyntaxKind.AbstractKeyword);
        if (!isInterfaceMember && !isAbstract) return;

        // Must be async or return Task/ValueTask
        var isAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword);
        if (!isAsync && !ReturnsTaskLike(method.ReturnType)) return;

        // Already has CancellationToken — check using 1-arg Contains (netstandard2.0 safe)
        foreach (var param in method.ParameterList.Parameters)
        {
            if (param.Type?.ToString().Contains("CancellationToken") == true) return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, method.Identifier.GetLocation(), method.Identifier.Text));
    }

    private static bool ReturnsTaskLike(TypeSyntax t)
    {
        var s = t.ToString();
        return s.StartsWith("Task", StringComparison.Ordinal)
            || s.StartsWith("ValueTask", StringComparison.Ordinal);
    }
}
