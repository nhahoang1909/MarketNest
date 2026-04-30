using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

/// <summary>
/// MN027 — Repository interfaces must not return IQueryable&lt;T&gt;.
/// Returning IQueryable leaks EF Core queries into the domain/application layer.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RepositoryIQueryableAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN027,
        title: "Repository interface must not return IQueryable<T>",
        messageFormat: "Method '{0}' in repository interface '{1}' returns IQueryable — this leaks EF Core into the domain layer",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.MethodDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var method = (MethodDeclarationSyntax)context.Node;

        // Only apply to interfaces with "Repository" in the name
        var containingInterface = method.Ancestors()
            .OfType<InterfaceDeclarationSyntax>().FirstOrDefault();
        if (containingInterface is null) return;

        var interfaceName = containingInterface.Identifier.Text;
        if (!interfaceName.Contains("Repository")) return;

        if (ReturnsIQueryable(method.ReturnType, context.SemanticModel))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule, method.ReturnType.GetLocation(), method.Identifier.Text, interfaceName));
        }
    }

    private static bool ReturnsIQueryable(TypeSyntax returnType, SemanticModel model)
    {
        var typeInfo = model.GetTypeInfo(returnType);
        return IsOrContainsIQueryable(typeInfo.Type);
    }

    private static bool IsOrContainsIQueryable(ITypeSymbol? type)
    {
        if (type is null) return false;

        if (type.OriginalDefinition.Name == "IQueryable") return true;

        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            foreach (var arg in named.TypeArguments)
            {
                if (IsOrContainsIQueryable(arg)) return true;
            }
        }

        return false;
    }
}

