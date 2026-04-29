using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InsecureHashAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN018,
        title: "Insecure hash algorithm detected",
        messageFormat: "Hash algorithm '{0}' is cryptographically weak. Use 'SHA512' or higher instead.",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly string[] InsecureAlgorithms = ["MD5", "SHA256"];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        // Match patterns like: MD5.Create(), MD5.HashData(), SHA256.Create(), SHA256.HashData()
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        var methodName = memberAccess.Name.Identifier.Text;

        // Only check if this is a hash-related method call
        if (!IsHashMethod(methodName))
            return;

        // Check if the type being accessed is an insecure hash algorithm
        if (memberAccess.Expression is IdentifierNameSyntax typeIdentifier)
        {
            if (IsInsecureAlgorithmName(typeIdentifier.Identifier.Text))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    typeIdentifier.GetLocation(),
                    typeIdentifier.Identifier.Text));
            }
        }
    }

    private static bool IsHashMethod(string methodName)
    {
        return methodName == "Create"
            || methodName == "HashData"
            || methodName == "ComputeHash"
            || methodName == "GetHash"
            || methodName == "Hash";
    }

    private static bool IsInsecureAlgorithmName(string name)
    {
        return InsecureAlgorithms.Contains(name, StringComparer.Ordinal);
    }
}

