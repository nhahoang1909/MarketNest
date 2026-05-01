using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

/// <summary>
/// MN034 — CommandHandler must not inject query-side types.
/// A class that handles writes (implements ICommandHandler) must not also depend on
/// <c>I*Query</c> interfaces or any <c>IQueryHandler</c> — mixing read / write side
/// in the same class makes dependencies hard to reason about and test.
/// If both sides need shared logic, extract it to a dedicated helper class.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CommandHandlerQueryInjectionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN034,
        title: "CommandHandler must not inject query-side types",
        messageFormat: "CommandHandler '{0}' injects query-side type '{1}' — " +
                       "extract shared logic to a helper class instead of mixing read/write sides",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzePrimaryConstructor, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ConstructorDeclaration);
    }

    private static void AnalyzePrimaryConstructor(SyntaxNodeAnalysisContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        if (classDecl.ParameterList is null) return;
        if (!IsCommandHandler(classDecl, context.SemanticModel)) return;

        foreach (var parameter in classDecl.ParameterList.Parameters)
            CheckParameter(parameter, classDecl.Identifier.Text, context);
    }

    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
    {
        var ctor = (ConstructorDeclarationSyntax)context.Node;
        var containingClass = ctor.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass is null) return;
        if (!IsCommandHandler(containingClass, context.SemanticModel)) return;

        foreach (var parameter in ctor.ParameterList.Parameters)
            CheckParameter(parameter, containingClass.Identifier.Text, context);
    }

    private static void CheckParameter(ParameterSyntax parameter, string className, SyntaxNodeAnalysisContext context)
    {
        if (parameter.Type is null) return;

        var typeInfo = context.SemanticModel.GetTypeInfo(parameter.Type);
        if (typeInfo.Type is not INamedTypeSymbol paramType) return;

        if (IsQuerySideType(paramType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule, parameter.Type.GetLocation(), className, paramType.Name));
        }
    }

    /// <summary>
    /// Returns true for:
    ///  - any interface whose name ends with "Query" (e.g., IGetOrdersQuery, IOrderQuery)
    ///  - any interface named IQueryHandler or IQueryHandler&lt;&gt;
    /// </summary>
    private static bool IsQuerySideType(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface)
        {
            var name = type.OriginalDefinition.Name;
            if (name == "IQueryHandler") return true;
            if (name.StartsWith("I", System.StringComparison.Ordinal)
                && name.EndsWith("Query", System.StringComparison.Ordinal)) return true;
        }

        // Concrete QueryHandler injected (very unusual but still forbidden)
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.OriginalDefinition.Name == "IQueryHandler") return true;
        }

        return false;
    }

    private static bool IsCommandHandler(ClassDeclarationSyntax classDecl, SemanticModel model)
    {
        if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return false;

        foreach (var iface in classSymbol.AllInterfaces)
        {
            if (iface.OriginalDefinition.Name == "ICommandHandler") return true;
        }
        return false;
    }
}

