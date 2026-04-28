// src/MarketNest.Analyzers/Analyzers/Naming/PrivateFieldNamingAnalyzer.cs
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Naming;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrivateFieldNamingAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN001,
        title: "Private field must use _camelCase",
        messageFormat: "Private field '{0}' must use _camelCase naming (prefix with underscore, first letter lowercase)",
        category: "Naming",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Private fields must follow the _camelCase convention (section 2.2).");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.FieldDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var field = (FieldDeclarationSyntax)context.Node;
        var modifiers = field.Modifiers;

        if (!modifiers.Any(SyntaxKind.PrivateKeyword)) return;
        if (modifiers.Any(SyntaxKind.ConstKeyword)) return;
        if (modifiers.Any(SyntaxKind.StaticKeyword) && modifiers.Any(SyntaxKind.ReadOnlyKeyword)) return;

        foreach (var variable in field.Declaration.Variables)
        {
            var name = variable.Identifier.Text;
            if (!IsValidName(name))
                context.ReportDiagnostic(Diagnostic.Create(Rule, variable.Identifier.GetLocation(), name));
        }
    }

    private static bool IsValidName(string name) =>
        name == "_" || (name.Length >= 2 && name[0] == '_' && char.IsLower(name[1]));
}
