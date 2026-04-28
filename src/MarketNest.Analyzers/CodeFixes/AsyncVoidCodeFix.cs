// src/MarketNest.Analyzers/CodeFixes/AsyncVoidCodeFix.cs
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarketNest.Analyzers.AsyncRules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MarketNest.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AsyncVoidCodeFix)), Shared]
public sealed class AsyncVoidCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.MN003);
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var node = root.FindNode(context.Diagnostics[0].Location.SourceSpan);
        var method = node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Change return type to Task",
                createChangedDocument: ct => ChangeToTaskAsync(context.Document, method, ct),
                equivalenceKey: nameof(AsyncVoidCodeFix)),
            context.Diagnostics[0]);
    }

    private static async Task<Document> ChangeToTaskAsync(
        Document document, MethodDeclarationSyntax method, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null) return document;

        var taskType = SyntaxFactory.ParseTypeName("Task").WithTriviaFrom(method.ReturnType);
        var newMethod = method.WithReturnType(taskType);
        var newRoot = root.ReplaceNode(method, newMethod);
        return document.WithSyntaxRoot(newRoot);
    }
}
