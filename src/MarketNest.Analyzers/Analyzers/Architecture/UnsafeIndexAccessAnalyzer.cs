using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

/// <summary>
/// MN036 — Direct dictionary indexer access can throw <c>KeyNotFoundException</c> when the key
/// is absent, crashing the request with no graceful path.
/// <para>
/// Prefer <c>dict.TryGetValue(key, out var v)</c>, <c>dict.GetValueOrDefault(key)</c>,
/// or the project-specific <c>dict.TryGet(key)</c> extension from <c>CollectionExtensions</c>.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnsafeIndexAccessAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN036,
        title: "Unsafe dictionary indexer access",
        messageFormat: "Direct indexer access on '{0}' can throw KeyNotFoundException — use .TryGetValue(), .GetValueOrDefault(), or .TryGet() instead",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description:
            "Using dict[key] throws KeyNotFoundException when the key does not exist, " +
            "crashing the application. " +
            "Use TryGetValue(), GetValueOrDefault(), or the CollectionExtensions.TryGet() helper instead.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ElementAccessExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var elementAccess = (ElementAccessExpressionSyntax)context.Node;

        // Only flag single-argument indexer calls (dict[key], not multi-dim array[x, y])
        if (elementAccess.ArgumentList.Arguments.Count != 1)
            return;

        // Skip assignment targets: dict[key] = value  (left-hand side of assignment)
        // This is a *write* — setting a value is intentional and safe in this context.
        if (elementAccess.Parent is AssignmentExpressionSyntax assignment
            && assignment.Left == elementAccess)
        {
            return;
        }

        var typeInfo = context.SemanticModel.GetTypeInfo(elementAccess.Expression);
        if (typeInfo.Type is not INamedTypeSymbol type)
            return;

        if (IsDictionaryType(type))
        {
            var typeName = type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, elementAccess.GetLocation(), typeName));
        }
    }

    /// <summary>
    /// Returns true when the type is or implements <c>IDictionary&lt;,&gt;</c> or
    /// <c>IReadOnlyDictionary&lt;,&gt;</c> — the two dictionary contracts where the
    /// indexer throws on a missing key.
    /// </summary>
    private static bool IsDictionaryType(INamedTypeSymbol type)
    {
        // Check the type itself and all implemented interfaces
        var typesToCheck = new[] { type }.Concat(type.AllInterfaces);

        foreach (var t in typesToCheck)
        {
            if (!t.IsGenericType) continue;

            var original = t.OriginalDefinition.ToDisplayString();
            if (original == "System.Collections.Generic.IDictionary<TKey, TValue>"
                || original == "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")
            {
                return true;
            }
        }

        return false;
    }
}

