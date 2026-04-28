using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Logging;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LoggingClassPartialAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN006,
        title: "Logging class must be partial",
        messageFormat: "Class '{0}' injects IAppLogger<T> but is not declared as partial — add partial to enable [LoggerMessage] source generation",
        category: "Logging",
        defaultSeverity: DiagnosticSeverity.Error,
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
        if (classDecl.Modifiers.Any(SyntaxKind.PartialKeyword)) return;
        if (!HasAppLogger(classDecl, context.SemanticModel)) return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule, classDecl.Identifier.GetLocation(), classDecl.Identifier.Text));
    }

    private static bool HasAppLogger(ClassDeclarationSyntax classDecl, SemanticModel model)
    {
        if (classDecl.ParameterList is not null)
        {
            foreach (var param in classDecl.ParameterList.Parameters)
            {
                if (param.Type is null) continue;
                var typeInfo = model.GetTypeInfo(param.Type);
                if (IsAppLogger(typeInfo.Type)) return true;
            }
        }

        foreach (var field in classDecl.Members.OfType<FieldDeclarationSyntax>())
        {
            var typeInfo = model.GetTypeInfo(field.Declaration.Type);
            if (IsAppLogger(typeInfo.Type)) return true;
        }
        return false;
    }

    internal static bool IsAppLogger(ITypeSymbol? type)
    {
        var name = type?.OriginalDefinition?.ToDisplayString() ?? string.Empty;
        return name.IndexOf("IAppLogger", StringComparison.Ordinal) >= 0;
    }
}
