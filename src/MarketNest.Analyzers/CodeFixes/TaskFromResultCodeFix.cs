using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MarketNest.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TaskFromResultCodeFix)), Shared]
public sealed class TaskFromResultCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.MN017);
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async System.Threading.Tasks.Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        if (root is null) return;

        var awaitExpr = root.FindNode(context.Diagnostics[0].Location.SourceSpan) as AwaitExpressionSyntax;
        if (awaitExpr is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Remove await Task.FromResult(...) wrapper",
                createChangedDocument: ct => UnwrapAsync(context.Document, awaitExpr, ct),
                equivalenceKey: nameof(TaskFromResultCodeFix)),
            context.Diagnostics[0]);
    }

    private static async System.Threading.Tasks.Task<Document> UnwrapAsync(
        Document document, AwaitExpressionSyntax awaitExpr, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct);
        if (root is null) return document;

        var invocation = (InvocationExpressionSyntax)awaitExpr.Expression;
        var argument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
        if (argument is null) return document;

        var replacement = argument.WithTriviaFrom(awaitExpr);
        return document.WithSyntaxRoot(root.ReplaceNode(awaitExpr, replacement));
    }
}
