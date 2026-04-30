using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.AsyncRules;

/// <summary>
/// MN023 — Fire-and-forget async calls are banned. Calling an async method without
/// awaiting it silently swallows exceptions. All Task-returning method invocations
/// must be awaited, assigned, or explicitly discarded with a comment.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FireAndForgetAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN023,
        title: "Fire-and-forget async call",
        messageFormat: "Async method '{0}' is called without await — this silently ignores exceptions",
        category: "AsyncRules",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ExpressionStatement);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var exprStatement = (ExpressionStatementSyntax)context.Node;

        // We look for expression statements that are simply invocations (no await)
        if (exprStatement.Expression is not InvocationExpressionSyntax invocation) return;

        // Check if the return type is Task or Task<T> or ValueTask or ValueTask<T>
        var typeInfo = context.SemanticModel.GetTypeInfo(invocation);
        if (typeInfo.Type is null) return;

        if (IsTaskLikeType(typeInfo.Type))
        {
            var methodName = GetMethodName(invocation);
            context.ReportDiagnostic(Diagnostic.Create(
                Rule, invocation.GetLocation(), methodName));
        }
    }

    private static bool IsTaskLikeType(ITypeSymbol type)
    {
        var name = type.Name;
        if (name == "Task" || name == "ValueTask") return true;

        if (type.OriginalDefinition is INamedTypeSymbol named)
        {
            var origName = named.Name;
            if (origName == "Task" || origName == "ValueTask") return true;
        }

        return false;
    }

    private static string GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            IdentifierNameSyntax id => id.Identifier.Text,
            _ => invocation.Expression.ToString()
        };
    }
}

