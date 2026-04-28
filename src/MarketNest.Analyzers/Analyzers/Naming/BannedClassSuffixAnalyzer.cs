// src/MarketNest.Analyzers/Analyzers/Naming/BannedClassSuffixAnalyzer.cs
using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Naming;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BannedClassSuffixAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] BannedSuffixes = new string[] { "Manager", "Helper", "Utils" };

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN002,
        title: "Banned class suffix",
        messageFormat: "Class '{0}' uses banned suffix '{1}' — use a more descriptive name",
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

        foreach (var suffix in BannedSuffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule, classDecl.Identifier.GetLocation(), name, suffix));
                return;
            }
        }
    }
}
