using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

/// <summary>
/// MN033 — ICacheService/CacheKeys must not be used in the Domain layer.
/// Caching is an infrastructure concern and should only be in Application or Infrastructure layers.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DomainCacheUsageAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> CacheTypes =
        ImmutableHashSet.Create(StringComparer.Ordinal,
            "ICacheService", "CacheKeys", "IDistributedCache", "IMemoryCache");

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN033,
        title: "Cache usage in Domain layer",
        messageFormat: "'{0}' must not be used in the Domain layer — caching is an infrastructure concern",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeIdentifier, SyntaxKind.IdentifierName);
    }

    private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
    {
        var identifier = (IdentifierNameSyntax)context.Node;
        var name = identifier.Identifier.Text;

        if (!CacheTypes.Contains(name)) return;
        if (!IsInDomainNamespace(identifier)) return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule, identifier.GetLocation(), name));
    }

    private static bool IsInDomainNamespace(SyntaxNode node)
    {
        // Check file-scoped namespace
        var root = node.SyntaxTree.GetRoot();
        var nsDecl = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        if (nsDecl is null) return false;

        var nsName = nsDecl.Name.ToString();
        return nsName.Contains(".Domain");
    }
}

