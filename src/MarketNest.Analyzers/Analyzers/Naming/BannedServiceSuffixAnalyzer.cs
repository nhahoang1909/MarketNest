using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Naming;

/// <summary>
/// MN021 — Concrete classes must not use the "Service" suffix unless they implement
/// a matching "I{Name}Service" interface. The suffix is too generic and masks intent.
/// Interfaces named I*Service are exempt.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BannedServiceSuffixAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN021,
        title: "Banned 'Service' suffix on concrete class",
        messageFormat: "Class '{0}' uses the generic 'Service' suffix — rename to describe its actual responsibility, or implement a matching interface",
        category: "Naming",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;
        var name = classDecl.Identifier.Text;

        if (!name.EndsWith("Service", System.StringComparison.Ordinal)) return;
        if (classDecl.Modifiers.Any(SyntaxKind.AbstractKeyword)) return;

        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return;

        // Exempt if the class implements a matching I*Service interface
        var expectedInterface = "I" + name;
        foreach (var iface in classSymbol.AllInterfaces)
        {
            if (iface.Name == expectedInterface) return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, classDecl.Identifier.GetLocation(), name));
    }
}

