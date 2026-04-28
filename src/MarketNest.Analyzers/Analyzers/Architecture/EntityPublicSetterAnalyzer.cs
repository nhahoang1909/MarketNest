using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EntityPublicSetterAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN016,
        title: "Entity/Aggregate property must not have public setter",
        messageFormat: "Property '{0}' on Entity/AggregateRoot has a public setter — use private set and mutate via domain methods",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.PropertyDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var property = (PropertyDeclarationSyntax)context.Node;
        var setter = property.AccessorList?.Accessors
            .FirstOrDefault(a => a.IsKind(SyntaxKind.SetAccessorDeclaration));
        if (setter is null) return;

        var setterHasNoModifier = !setter.Modifiers.Any();
        var propertyIsPublic = property.Modifiers.Any(SyntaxKind.PublicKeyword);
        if (!(setterHasNoModifier && propertyIsPublic)) return;

        var containingClass = property.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass is null) return;

        if (context.SemanticModel.GetDeclaredSymbol(containingClass) is not INamedTypeSymbol classSymbol)
            return;

        if (InheritsFromEntityOrAggregate(classSymbol))
            context.ReportDiagnostic(Diagnostic.Create(
                Rule, property.Identifier.GetLocation(), property.Identifier.Text));
    }

    private static bool InheritsFromEntityOrAggregate(INamedTypeSymbol symbol)
    {
        for (var t = symbol.BaseType; t is not null; t = t.BaseType)
        {
            var name = t.OriginalDefinition.Name;
            if (name == "Entity" || name == "AggregateRoot") return true;
        }
        return false;
    }
}
