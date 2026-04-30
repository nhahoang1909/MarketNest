using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

/// <summary>
/// MN026 — Domain layer must not reference infrastructure namespaces.
/// Files in *.Domain namespaces must not use `using Microsoft.EntityFrameworkCore`,
/// `using StackExchange.Redis`, `using System.Net.Http`, etc.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DomainInfrastructureReferenceAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] BannedNamespacePrefixes =
    {
        "Microsoft.EntityFrameworkCore",
        "StackExchange.Redis",
        "System.Net.Http",
        "Microsoft.AspNetCore",
        "Npgsql",
        "MassTransit",
        "RabbitMQ"
    };

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN026,
        title: "Domain layer must not reference infrastructure namespaces",
        messageFormat: "Domain layer file uses banned infrastructure namespace '{0}' — domain must have no infrastructure dependencies",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.UsingDirective);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var usingDirective = (UsingDirectiveSyntax)context.Node;
        if (usingDirective.Name is null) return;

        // Only apply in Domain layer namespaces
        var fileLevelNamespace = usingDirective.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();

        string? containingNamespace = null;
        if (fileLevelNamespace is not null)
        {
            containingNamespace = fileLevelNamespace.Name.ToString();
        }
        else
        {
            // Check compilation unit level — file-scoped namespace
            var compilationUnit = usingDirective.Ancestors()
                .OfType<CompilationUnitSyntax>().FirstOrDefault();
            if (compilationUnit is not null)
            {
                var fileNamespace = compilationUnit.Members
                    .OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
                if (fileNamespace is not null)
                    containingNamespace = fileNamespace.Name.ToString();
            }
        }

        if (containingNamespace is null) return;
        if (!containingNamespace.Contains(".Domain")) return;

        var usingName = usingDirective.Name.ToString();
        foreach (var banned in BannedNamespacePrefixes)
        {
            if (usingName.StartsWith(banned, StringComparison.Ordinal))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule, usingDirective.GetLocation(), usingName));
                return;
            }
        }
    }
}

