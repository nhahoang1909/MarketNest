using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace MarketNest.Analyzers.Architecture;

/// <summary>
/// MN024 — Command handlers must not call SaveChangesAsync() or CommitAsync() directly.
/// The transaction filter (RazorPageTransactionFilter / TransactionActionFilter) owns the
/// commit lifecycle. Only background jobs (outside HTTP pipeline) may commit manually.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HandlerSaveChangesAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> BannedMethods =
        ImmutableHashSet.Create(System.StringComparer.Ordinal,
            "SaveChanges", "SaveChangesAsync", "CommitAsync");

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN024,
        title: "Command handler must not call SaveChangesAsync/CommitAsync",
        messageFormat: "Handler '{0}' calls '{1}' directly — the transaction filter handles commit automatically (ADR-027)",
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

        if (IsCommandHandler(classSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule, invocation.GetLocation(), containingClass.Identifier.Text, methodName));
        }
    }

    private static bool IsCommandHandler(INamedTypeSymbol symbol)
    {
        foreach (var iface in symbol.AllInterfaces)
        {
            if (iface.OriginalDefinition.Name == "ICommandHandler") return true;
        }
        return false;
    }
}

