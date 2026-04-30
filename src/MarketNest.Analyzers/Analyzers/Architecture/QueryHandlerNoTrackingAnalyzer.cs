using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

/// <summary>
/// MN029 — Query handlers should use AsNoTracking() for all EF Core queries.
/// Detects terminal LINQ operators (ToListAsync, FirstOrDefaultAsync, etc.) in
/// IQueryHandler implementations without a preceding AsNoTracking() call.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QueryHandlerNoTrackingAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> TerminalOperators =
        ImmutableHashSet.Create(System.StringComparer.Ordinal,
            "ToList", "ToListAsync",
            "ToArray", "ToArrayAsync",
            "First", "FirstAsync",
            "FirstOrDefault", "FirstOrDefaultAsync",
            "Single", "SingleAsync",
            "SingleOrDefault", "SingleOrDefaultAsync",
            "Last", "LastAsync",
            "LastOrDefault", "LastOrDefaultAsync");

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN029,
        title: "Query handler should use AsNoTracking()",
        messageFormat: "'{0}' in query handler '{1}' materialises without AsNoTracking() — queries should always disable change tracking",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Warning,
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
        if (!TerminalOperators.Contains(methodName)) return;

        var containingClass = invocation.Ancestors()
            .OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass is null) return;

        if (context.SemanticModel.GetDeclaredSymbol(containingClass) is not INamedTypeSymbol classSymbol)
            return;

        if (!IsQueryHandler(classSymbol)) return;

        if (HasAsNoTrackingInChain(memberAccess.Expression)) return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule, memberAccess.Name.GetLocation(), methodName, containingClass.Identifier.Text));
    }

    private static bool IsQueryHandler(INamedTypeSymbol symbol)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.OriginalDefinition.Name == "IQueryHandler") return true;
        }

        for (var t = symbol.BaseType; t is not null; t = t.BaseType)
        {
            if (t.OriginalDefinition.Name == "BaseQuery") return true;
        }

        return false;
    }

    private static bool HasAsNoTrackingInChain(ExpressionSyntax expression)
    {
        var current = expression;
        while (true)
        {
            if (current is InvocationExpressionSyntax inv)
            {
                if (inv.Expression is MemberAccessExpressionSyntax ma)
                {
                    var name = ma.Name.Identifier.Text;
                    if (name == "AsNoTracking" || name == "AsNoTrackingWithIdentityResolution")
                        return true;
                    current = ma.Expression;
                }
                else break;
            }
            else if (current is MemberAccessExpressionSyntax memberAccess)
            {
                current = memberAccess.Expression;
            }
            else break;
        }
        return false;
    }
}

