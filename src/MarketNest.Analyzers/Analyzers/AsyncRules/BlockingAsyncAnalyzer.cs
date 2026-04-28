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
        messageFormat: "Blocking on async code via '{0}' can cause deadlocks — use await instead",
        category: "AsyncRules",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleMemberAccessExpression);
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
