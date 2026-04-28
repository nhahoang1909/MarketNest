using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MarketNest.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(LoggingClassPartialCodeFix)), Shared]
public sealed class LoggingClassPartialCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.MN006);
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var node = root.FindNode(context.Diagnostics[0].Location.SourceSpan);
        var classDecl = node.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (classDecl is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add 'partial' modifier",
                createChangedDocument: ct => AddPartialAsync(context.Document, classDecl, ct),
                equivalenceKey: nameof(LoggingClassPartialCodeFix)),
            context.Diagnostics[0]);
    }

    private static async Task<Document> AddPartialAsync(
        Document document, ClassDeclarationSyntax classDecl, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null) return document;

        // When no modifiers exist, the leading trivia lives on the 'class' keyword.
        // Move it to the new 'partial' token so formatting stays clean.
        SyntaxToken partialToken;
        SyntaxToken newClassKeyword = classDecl.Keyword;

        if (classDecl.Modifiers.Count == 0)
        {
            partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword)
                .WithLeadingTrivia(classDecl.Keyword.LeadingTrivia)
                .WithTrailingTrivia(SyntaxFactory.Space);
            newClassKeyword = classDecl.Keyword.WithLeadingTrivia(SyntaxFactory.TriviaList());
        }
        else
        {
            partialToken = SyntaxFactory.Token(SyntaxKind.PartialKeyword)
                .WithTrailingTrivia(SyntaxFactory.Space);
        }

        var newModifiers = classDecl.Modifiers.Add(partialToken);
        var newClass = classDecl
            .WithModifiers(newModifiers)
            .WithKeyword(newClassKeyword);
        return document.WithSyntaxRoot(root.ReplaceNode(classDecl, newClass));
    }
}
