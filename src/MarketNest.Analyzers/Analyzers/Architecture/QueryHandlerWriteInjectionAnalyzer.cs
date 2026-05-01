using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

/// <summary>
/// MN035 — QueryHandler must not inject write-side types or other QueryHandlers.
/// A class that handles reads (implements IQueryHandler) must not depend on:
///  - <c>I*Repository</c> interfaces (write-side persistence)
///  - <c>ICommandHandler</c> interfaces
///  - any concrete <c>CommandHandler</c> class
///  - any other <c>IQueryHandler</c> implementation (handler chaining)
///
/// For cross-aggregate reads within the same module, inject an <c>I*Query</c> interface instead.
/// For shared write+read logic, extract to a dedicated helper class.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class QueryHandlerWriteInjectionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor WriteSideRule = new(
        id: DiagnosticIds.MN035,
        title: "QueryHandler must not inject write-side types or other QueryHandlers",
        messageFormat: "QueryHandler '{0}' injects write-side or handler type '{1}' — " +
                       "use an I*Query interface for cross-aggregate reads, or extract shared logic to a helper class",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(WriteSideRule);

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
        if (!IsQueryHandler(classDecl, context.SemanticModel)) return;

        foreach (var parameter in classDecl.ParameterList.Parameters)
            CheckParameter(parameter, classDecl.Identifier.Text, context);
    }

    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
    {
        var ctor = (ConstructorDeclarationSyntax)context.Node;
        var containingClass = ctor.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass is null) return;
        if (!IsQueryHandler(containingClass, context.SemanticModel)) return;

        foreach (var parameter in ctor.ParameterList.Parameters)
            CheckParameter(parameter, containingClass.Identifier.Text, context);
    }

    private static void CheckParameter(ParameterSyntax parameter, string className, SyntaxNodeAnalysisContext context)
    {
        if (parameter.Type is null) return;

        var typeInfo = context.SemanticModel.GetTypeInfo(parameter.Type);
        if (typeInfo.Type is not INamedTypeSymbol paramType) return;

        if (IsWriteSideOrHandlerType(paramType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                WriteSideRule, parameter.Type.GetLocation(), className, paramType.Name));
        }
    }

    /// <summary>
    /// Returns true for:
    ///  - any interface whose name ends with "Repository" (e.g., IOrderRepository)
    ///  - ICommandHandler or ICommandHandler&lt;&gt;
    ///  - any concrete CommandHandler class (implements ICommandHandler)
    ///  - IQueryHandler or IQueryHandler&lt;&gt; (handler chaining is forbidden)
    ///  - any concrete QueryHandler class (implements IQueryHandler)
    /// </summary>
    private static bool IsWriteSideOrHandlerType(INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface)
        {
            var name = type.OriginalDefinition.Name;

            // Repository interfaces — write-side
            if (name.StartsWith("I", System.StringComparison.Ordinal)
                && name.EndsWith("Repository", System.StringComparison.Ordinal)) return true;

            // Handler interfaces
            if (name == "ICommandHandler" || name == "IQueryHandler") return true;
        }

        // Concrete handler classes
        foreach (var iface in type.AllInterfaces)
        {
            var ifaceName = iface.OriginalDefinition.Name;
            if (ifaceName == "ICommandHandler" || ifaceName == "IQueryHandler") return true;
        }

        return false;
    }

    private static bool IsQueryHandler(ClassDeclarationSyntax classDecl, SemanticModel model)
    {
        if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return false;

        foreach (var iface in classSymbol.AllInterfaces)
        {
            if (iface.OriginalDefinition.Name == "IQueryHandler") return true;
        }
        return false;
    }
}

