using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MarketNest.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(InsecureHashCodeFix)), Shared]
public sealed class InsecureHashCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(DiagnosticIds.MN018);

    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var node = root.FindNode(context.Diagnostics[0].Location.SourceSpan);

        // Try to find identifier or member access
        var identifier = node as IdentifierNameSyntax;
        var memberAccess = node as MemberAccessExpressionSyntax;

        string? insecureAlgorithm = null;
        IdentifierNameSyntax? targetIdentifier = null;

        if (identifier != null && IsInsecureAlgorithm(identifier.Identifier.Text))
        {
            insecureAlgorithm = identifier.Identifier.Text;
            targetIdentifier = identifier;
        }
        else if (memberAccess?.Expression is IdentifierNameSyntax exprIdentifier &&
                 IsInsecureAlgorithm(exprIdentifier.Identifier.Text))
        {
            insecureAlgorithm = exprIdentifier.Identifier.Text;
            targetIdentifier = exprIdentifier;
        }

        if (insecureAlgorithm is null || targetIdentifier is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Replace '{insecureAlgorithm}' with 'SHA512'",
                createChangedDocument: ct => ReplaceWithSha512Async(context.Document, targetIdentifier, ct),
                equivalenceKey: nameof(InsecureHashCodeFix)),
            context.Diagnostics[0]);
    }

    private static async Task<Document> ReplaceWithSha512Async(
        Document document, IdentifierNameSyntax identifier, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null) return document;

        var newIdentifier = identifier.WithIdentifier(
            SyntaxFactory.Identifier("SHA512"));

        return document.WithSyntaxRoot(root.ReplaceNode(identifier, newIdentifier));
    }

    private static bool IsInsecureAlgorithm(string name)
    {
        return name == "MD5" || name == "SHA256";
    }
}

