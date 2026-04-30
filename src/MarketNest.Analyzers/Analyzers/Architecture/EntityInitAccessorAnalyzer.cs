using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

/// <summary>
/// MN028 — Entity/AggregateRoot properties must not use { get; init; } accessor.
/// Init-only setters bypass domain method guards and violate DDD encapsulation (ADR-007).
/// Use { get; private set; } and mutate via domain methods instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EntityInitAccessorAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN028,
        title: "Entity property must not use init accessor",
        messageFormat: "Property '{0}' on Entity/AggregateRoot uses 'init' accessor — use 'private set' and mutate via domain methods (ADR-007)",
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
        var initAccessor = property.AccessorList?.Accessors
            .FirstOrDefault(a => a.IsKind(SyntaxKind.InitAccessorDeclaration));
        if (initAccessor is null) return;

        var containingClass = property.Ancestors()
            .OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass is null) return;

        if (context.SemanticModel.GetDeclaredSymbol(containingClass) is not INamedTypeSymbol classSymbol)
            return;

        if (InheritsFromEntityOrAggregate(classSymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule, property.Identifier.GetLocation(), property.Identifier.Text));
        }
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

