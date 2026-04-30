using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

/// <summary>
/// MN020 — QueryHandlers (IQueryHandler and BaseQuery subclasses) should include a
/// .Select() / .SelectMany() projection in every terminal LINQ call so that only
/// the needed columns are loaded from the database.
///
/// CommandHandlers are intentionally excluded — they often need full entity state
/// to enforce domain invariants before mutating.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HandlerQueryProjectionAnalyzer : DiagnosticAnalyzer
{
    // Terminal LINQ operators that materialise the query result.
    // AnyAsync / CountAsync / SumAsync etc. do NOT load entity columns, so they are excluded.
    private static readonly ImmutableHashSet<string> TerminalOperators =
        ImmutableHashSet.Create(StringComparer.Ordinal,
            "ToList", "ToListAsync",
            "ToArray", "ToArrayAsync",
            "ToDictionary", "ToDictionaryAsync",
            "First", "FirstAsync",
            "FirstOrDefault", "FirstOrDefaultAsync",
            "Single", "SingleAsync",
            "SingleOrDefault", "SingleOrDefaultAsync",
            "Last", "LastAsync",
            "LastOrDefault", "LastOrDefaultAsync");

    private static readonly ImmutableHashSet<string> ProjectionOperators =
        ImmutableHashSet.Create(StringComparer.Ordinal, "Select", "SelectMany");

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN020,
        title: "QueryHandler query is missing a Select projection",
        messageFormat: "'{0}' in '{1}' materialises the query without a .Select() projection — project to a DTO to avoid loading unnecessary columns",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "QueryHandlers and BaseQuery subclasses should always project to a DTO with .Select() before materialising a query with ToList/FirstOrDefault/etc. " +
                     "This prevents loading every entity column from the database. " +
                     "CommandHandlers may suppress this rule (MN020) when they legitimately need the full aggregate.");

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

        // Only in classes that implement IQueryHandler or inherit from BaseQuery
        var containingClass = invocation.Ancestors()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault();
        if (containingClass is null) return;

        if (context.SemanticModel.GetDeclaredSymbol(containingClass) is not INamedTypeSymbol classSymbol)
            return;

        if (!IsQueryHandlerOrBaseQuery(classSymbol)) return;

        // Walk the invocation chain backwards looking for .Select() / .SelectMany()
        if (HasProjectionInChain(memberAccess.Expression)) return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            memberAccess.Name.GetLocation(),
            methodName,
            containingClass.Identifier.Text));
    }

    private static bool IsQueryHandlerOrBaseQuery(INamedTypeSymbol symbol)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.OriginalDefinition.Name == "IQueryHandler") return true;
        }

        // BaseQuery<TEntity, TKey, TContext> subclasses also qualify
        for (var t = symbol.BaseType; t is not null; t = t.BaseType)
        {
            if (t.OriginalDefinition.Name == "BaseQuery") return true;
        }

        return false;
    }

    /// <summary>
    /// Walks the fluent call chain (e.g. <c>_db.Orders.Where(…).Select(…).ToListAsync()</c>)
    /// and returns <c>true</c> if a projection operator appears anywhere in the chain.
    /// </summary>
    private static bool HasProjectionInChain(ExpressionSyntax expression)
    {
        var current = expression;
        while (true)
        {
            if (current is InvocationExpressionSyntax inv)
            {
                if (inv.Expression is MemberAccessExpressionSyntax ma)
                {
                    if (ProjectionOperators.Contains(ma.Name.Identifier.Text)) return true;
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

