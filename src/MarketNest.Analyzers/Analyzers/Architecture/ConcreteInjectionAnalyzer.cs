using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

/// <summary>
/// MN030 — Constructor parameters should be interfaces (or abstract classes), not concrete types.
/// Injecting concrete classes instead of interfaces violates DI best practices and makes
/// testing difficult.
/// Exempts: primitives, strings, value types, records, loggers, and framework types.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConcreteInjectionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN030,
        title: "Inject interface instead of concrete class",
        messageFormat: "Parameter '{0}' in '{1}' injects concrete type '{2}' — use an interface instead",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Warning,
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
        if (!IsHandlerOrPageModel(classDecl, context.SemanticModel)) return;

        foreach (var parameter in classDecl.ParameterList.Parameters)
        {
            CheckParameter(parameter, classDecl.Identifier.Text, context);
        }
    }

    private static void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
    {
        var ctor = (ConstructorDeclarationSyntax)context.Node;
        var containingClass = ctor.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass is null) return;
        if (!IsHandlerOrPageModel(containingClass, context.SemanticModel)) return;

        foreach (var parameter in ctor.ParameterList.Parameters)
        {
            CheckParameter(parameter, containingClass.Identifier.Text, context);
        }
    }

    private static void CheckParameter(ParameterSyntax parameter, string className, SyntaxNodeAnalysisContext context)
    {
        if (parameter.Type is null) return;

        var typeInfo = context.SemanticModel.GetTypeInfo(parameter.Type);
        if (typeInfo.Type is not INamedTypeSymbol paramType) return;

        // Skip interfaces, abstract classes, value types, enums, delegates, records
        if (paramType.TypeKind == TypeKind.Interface) return;
        if (paramType.TypeKind == TypeKind.Enum) return;
        if (paramType.TypeKind == TypeKind.Delegate) return;
        if (paramType.IsAbstract) return;
        if (paramType.IsValueType) return;
        if (paramType.IsRecord) return;

        // Skip common exempt types
        var name = paramType.Name;
        if (name == "String" || name == "CancellationToken") return;

        // Skip types from System namespace (options, etc.)
        var ns = paramType.ContainingNamespace?.ToDisplayString() ?? "";
        if (ns.StartsWith("System", System.StringComparison.Ordinal)) return;
        if (ns.StartsWith("Microsoft.Extensions", System.StringComparison.Ordinal)) return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule, parameter.Type.GetLocation(), parameter.Identifier.Text, className, paramType.Name));
    }

    private static bool IsHandlerOrPageModel(ClassDeclarationSyntax classDecl, SemanticModel model)
    {
        if (model.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return false;

        for (var t = classSymbol.BaseType; t is not null; t = t.BaseType)
            if (t.Name == "PageModel") return true;

        foreach (var iface in classSymbol.AllInterfaces)
        {
            var n = iface.OriginalDefinition.Name;
            if (n == "ICommandHandler" || n == "IQueryHandler") return true;
        }
        return false;
    }
}

