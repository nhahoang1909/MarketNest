using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FlatNamespaceAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN008,
        title: "Namespace must be flat at layer level",
        messageFormat: "Namespace '{0}' exceeds three segments — namespaces must stop at 'MarketNest.<Module>.<Layer>'",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze,
            SyntaxKind.NamespaceDeclaration,
            SyntaxKind.FileScopedNamespaceDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        string? name;
        Location location;
        switch (context.Node)
        {
            case NamespaceDeclarationSyntax n:
                name = n.Name.ToString();
                location = n.Name.GetLocation();
                break;
            case FileScopedNamespaceDeclarationSyntax f:
                name = f.Name.ToString();
                // Report on keyword+name+semicolon span to match {|MN008:namespace X;|} markup
                location = Location.Create(
                    f.SyntaxTree,
                    Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(
                        f.NamespaceKeyword.SpanStart,
                        f.SemicolonToken.Span.End));
                break;
            default:
                return;
        }

        if (!name.StartsWith("MarketNest.", StringComparison.Ordinal)) return;
        if (name.Split('.').Length > 3)
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
    }
}
