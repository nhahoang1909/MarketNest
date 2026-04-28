using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DateTimeUsageAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN009,
        title: "Use DateTimeOffset instead of DateTime",
        messageFormat: "Use 'DateTimeOffset' instead of 'DateTime' to preserve timezone information",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.FieldDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        TypeSyntax? typeSyntax = context.Node switch
        {
            PropertyDeclarationSyntax p => p.Type,
            FieldDeclarationSyntax f => f.Declaration.Type,
            _ => null
        };
        if (typeSyntax is null) return;

        var typeInfo = context.SemanticModel.GetTypeInfo(typeSyntax);
        if (IsDateTime(typeInfo.Type))
            context.ReportDiagnostic(Diagnostic.Create(Rule, typeSyntax.GetLocation()));
    }

    private static bool IsDateTime(ITypeSymbol? type)
    {
        if (type is null) return false;
        if (type.SpecialType == SpecialType.System_DateTime) return true;
        if (type is INamedTypeSymbol named &&
            named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return named.TypeArguments.FirstOrDefault()?.SpecialType == SpecialType.System_DateTime;
        return false;
    }
}
