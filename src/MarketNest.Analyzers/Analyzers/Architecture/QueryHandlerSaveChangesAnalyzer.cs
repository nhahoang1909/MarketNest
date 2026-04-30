using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

/// <summary>
/// MN031 — Query handlers must never call SaveChanges/SaveChangesAsync.
/// Queries are strictly read-only operations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QueryHandlerSaveChangesAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> BannedMethods =
        ImmutableHashSet.Create(System.StringComparer.Ordinal,
            "SaveChanges", "SaveChangesAsync", "CommitAsync");

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN031,
        title: "Query handler must not call SaveChanges",
        messageFormat: "Query handler '{0}' calls '{1}' — queries must be strictly read-only",
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
        string methodName;

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            methodName = memberAccess.Name.Identifier.Text;
        else if (invocation.Expression is IdentifierNameSyntax identifier)
            methodName = identifier.Identifier.Text;
        else
            return;

        if (!BannedMethods.Contains(methodName)) return;

        var containingClass = invocation.Ancestors()
            .OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass is null) return;

        if (context.SemanticModel.GetDeclaredSymbol(containingClass) is not INamedTypeSymbol classSymbol)
            return;

        if (IsQueryHandler(classSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule, invocation.GetLocation(), containingClass.Identifier.Text, methodName));
        }
    }

    private static bool IsQueryHandler(INamedTypeSymbol symbol)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.OriginalDefinition.Name == "IQueryHandler") return true;
        }
        return false;
    }
}

