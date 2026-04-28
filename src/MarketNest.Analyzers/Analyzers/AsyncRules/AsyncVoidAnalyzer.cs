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
        messageFormat: "Method '{0}' must not be async void — use async Task instead",
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

        if (method.ReturnType is PredefinedTypeSyntax pts && pts.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            // skip — it is void, continue checking
        }
        else
        {
            return;
        }

        foreach (var param in method.ParameterList.Parameters)
        {
            var typeName = param.Type?.ToString() ?? string.Empty;
            if (typeName.EndsWith("EventArgs", System.StringComparison.Ordinal)) return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, method.Identifier.GetLocation(), method.Identifier.Text));
    }
}
