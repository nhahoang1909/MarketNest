using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Naming;

/// <summary>
/// MN022 — Class names ending in "Impl" are banned. The suffix adds no value
/// and violates the naming rule in code-rules.md §2.2.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BannedImplSuffixAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN022,
        title: "Banned 'Impl' class suffix",
        messageFormat: "Class '{0}' uses the banned 'Impl' suffix — use a descriptive name instead (e.g., 'SqlOrderRepository')",
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

        if (name.EndsWith("Impl", System.StringComparison.Ordinal))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule, classDecl.Identifier.GetLocation(), name));
        }
    }
}

