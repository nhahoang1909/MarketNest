using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ServiceLocatorAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> ServiceLocatorMethods =
        ImmutableHashSet.Create(StringComparer.Ordinal, "GetService", "GetRequiredService");

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN010,
        title: "Service locator anti-pattern",
        messageFormat: "Do not use '{0}' inside handlers or PageModels — declare dependencies in the constructor instead",
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
        if (!ServiceLocatorMethods.Contains(methodName)) return;

        var containingClass = invocation.Ancestors()
            .OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass is null) return;

        if (context.SemanticModel.GetDeclaredSymbol(containingClass) is not INamedTypeSymbol classSymbol)
            return;

        if (IsHandlerOrPageModel(classSymbol))
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation(), methodName));
    }

    private static bool IsHandlerOrPageModel(INamedTypeSymbol symbol)
    {
        for (var t = symbol.BaseType; t is not null; t = t.BaseType)
            if (t.Name == "PageModel") return true;

        foreach (var iface in symbol.AllInterfaces)
        {
            var n = iface.OriginalDefinition.Name;
            if (n == "ICommandHandler" || n == "IQueryHandler") return true;
        }
        return false;
    }
}
