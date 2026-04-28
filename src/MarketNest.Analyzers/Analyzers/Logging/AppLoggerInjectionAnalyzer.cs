using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Logging;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AppLoggerInjectionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN007,
        title: "Must inject IAppLogger<T> not ILogger<T>",
        messageFormat: "Inject 'IAppLogger<T>' instead of '{0}' — IAppLogger wraps ILogger and is required by the project logging standard",
        category: "Logging",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeParameter, SyntaxKind.Parameter);
        context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.FieldDeclaration);
    }

    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context)
    {
        var param = (ParameterSyntax)context.Node;
        if (param.Type is null) return;

        var typeInfo = context.SemanticModel.GetTypeInfo(param.Type);
        var typeName = typeInfo.Type?.OriginalDefinition?.ToDisplayString() ?? string.Empty;

        if (typeName.IndexOf("Microsoft.Extensions.Logging.ILogger<", StringComparison.Ordinal) >= 0)
            context.ReportDiagnostic(Diagnostic.Create(Rule, param.Type.GetLocation(), typeName));
    }

    private static void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        var typeInfo = context.SemanticModel.GetTypeInfo(field.Declaration.Type);
        var typeName = typeInfo.Type?.OriginalDefinition?.ToDisplayString() ?? string.Empty;

        if (typeName.IndexOf("Microsoft.Extensions.Logging.ILogger<", StringComparison.Ordinal) >= 0)
            context.ReportDiagnostic(Diagnostic.Create(Rule, field.Declaration.Type.GetLocation(), typeName));
    }
}
