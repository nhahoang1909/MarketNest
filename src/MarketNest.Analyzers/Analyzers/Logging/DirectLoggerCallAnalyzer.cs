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
        messageFormat: "Do not call '{0}' directly on ILogger — use [LoggerMessage] source-generated delegates via a nested 'Log' class",
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

        if (typeName.IndexOf("ILogger", StringComparison.Ordinal) < 0) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), methodName));
    }
}
