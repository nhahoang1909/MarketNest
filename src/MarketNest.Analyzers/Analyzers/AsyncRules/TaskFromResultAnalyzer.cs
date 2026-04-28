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
        messageFormat: "'await Task.FromResult(...)' is redundant — return the value directly",
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
        if (typeName.IndexOf("Task", System.StringComparison.Ordinal) < 0) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, awaitExpr.GetLocation()));
    }
}
