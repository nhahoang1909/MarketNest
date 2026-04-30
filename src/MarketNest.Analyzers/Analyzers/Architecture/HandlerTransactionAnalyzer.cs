using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

/// <summary>
/// MN025 — Command/Query handlers must not call BeginTransactionAsync or BeginTransaction.
/// Transaction management is handled by infrastructure filters (ADR-027).
/// Only background jobs (IBackgroundJob) may manage transactions explicitly.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HandlerTransactionAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> BannedMethods =
        ImmutableHashSet.Create(System.StringComparer.Ordinal,
            "BeginTransaction", "BeginTransactionAsync");

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN025,
        title: "Handler must not manage transactions directly",
        messageFormat: "Handler '{0}' calls '{1}' — transaction management is owned by infrastructure filters (ADR-027)",
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
        if (!BannedMethods.Contains(methodName)) return;

        var containingClass = invocation.Ancestors()
            .OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass is null) return;

        if (context.SemanticModel.GetDeclaredSymbol(containingClass) is not INamedTypeSymbol classSymbol)
            return;

        if (IsHandler(classSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule, invocation.GetLocation(), containingClass.Identifier.Text, methodName));
        }
    }

    private static bool IsHandler(INamedTypeSymbol symbol)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            var name = iface.OriginalDefinition.Name;
            if (name == "ICommandHandler" || name == "IQueryHandler") return true;
        }
        return false;
    }
}

