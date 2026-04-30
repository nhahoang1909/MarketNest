using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

/// <summary>
/// MN032 — .Include()/.ThenInclude() chains must not exceed 3 levels deep.
/// Deep include chains cause cartesian explosion and performance degradation.
/// Use dedicated queries or projections (.Select()) instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DeepIncludeChainAnalyzer : DiagnosticAnalyzer
{
    private const int MaxThenIncludeDepth = 2; // 1 Include + 2 ThenInclude = 3 levels total

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN032,
        title: ".Include() chain too deep",
        messageFormat: "Include chain is {0} levels deep (max 3) — use a dedicated query with .Select() projection instead",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return;

        var methodName = memberAccess.Name.Identifier.Text;
        if (methodName != "ThenInclude") return;

        // Count consecutive ThenInclude depth from this node
        int depth = CountThenIncludeDepth(invocation);

        if (depth > MaxThenIncludeDepth)
        {
            // depth + 1 for the initial Include
            context.ReportDiagnostic(Diagnostic.Create(
                Rule, memberAccess.Name.GetLocation(), depth + 1));
        }
    }

    private static int CountThenIncludeDepth(InvocationExpressionSyntax startInvocation)
    {
        int depth = 0;
        var current = startInvocation;

        while (current is not null)
        {
            if (current.Expression is MemberAccessExpressionSyntax ma)
            {
                var name = ma.Name.Identifier.Text;
                if (name == "ThenInclude")
                {
                    depth++;
                    // Walk up the chain
                    if (ma.Expression is InvocationExpressionSyntax parentInv)
                        current = parentInv;
                    else
                        break;
                }
                else if (name == "Include")
                {
                    break; // Stop at Include — we found the root
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }

        return depth;
    }
}

