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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AppLoggerInjectionCodeFix)), Shared]
public sealed class AppLoggerInjectionCodeFix : CodeFixProvider
{
    private const string AppLoggerNamespace = "MarketNest.Base.Infrastructure";

    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(DiagnosticIds.MN007);
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var node = root.FindNode(context.Diagnostics[0].Location.SourceSpan);
        if (node is not GenericNameSyntax genericName) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Replace with IAppLogger<T>",
                createChangedDocument: ct => ReplaceAsync(context.Document, genericName, ct),
                equivalenceKey: nameof(AppLoggerInjectionCodeFix)),
            context.Diagnostics[0]);
    }

    private static async Task<Document> ReplaceAsync(
        Document document, GenericNameSyntax oldType, CancellationToken ct)
    {
        var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
        if (root is null) return document;

        var typeArg = oldType.TypeArgumentList.Arguments.FirstOrDefault();
        if (typeArg is null) return document;

        var newTypeName = SyntaxFactory.GenericName(
            SyntaxFactory.Identifier("IAppLogger"),
            SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(typeArg)))
            .WithTriviaFrom(oldType);

        var newRoot = root.ReplaceNode(oldType, newTypeName);
        if (newRoot is CompilationUnitSyntax cu)
            newRoot = AddUsingIfMissing(cu, AppLoggerNamespace);
        return document.WithSyntaxRoot(newRoot);
    }

    private static CompilationUnitSyntax AddUsingIfMissing(CompilationUnitSyntax root, string ns)
    {
        if (root.Usings.Any(u => u.Name != null && u.Name.ToString() == ns)) return root;
        var directive = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ns))
            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
        return root.AddUsings(directive);
    }
}
