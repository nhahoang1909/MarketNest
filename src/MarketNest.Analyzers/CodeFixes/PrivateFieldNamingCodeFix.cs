// src/MarketNest.Analyzers/CodeFixes/PrivateFieldNamingCodeFix.cs
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace MarketNest.Analyzers.CodeFixes;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PrivateFieldNamingCodeFix)), Shared]
public sealed class PrivateFieldNamingCodeFix : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(DiagnosticIds.MN001);

    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var token = root.FindToken(context.Diagnostics[0].Location.SourceSpan.Start);
        if (token.Parent is not VariableDeclaratorSyntax declarator) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Rename to _camelCase",
                createChangedSolution: ct => RenameAsync(context.Document, declarator, ct),
                equivalenceKey: nameof(PrivateFieldNamingCodeFix)),
            context.Diagnostics[0]);
    }

    private static async Task<Solution> RenameAsync(
        Document document, VariableDeclaratorSyntax declarator, CancellationToken ct)
    {
        var semanticModel = await document.GetSemanticModelAsync(ct).ConfigureAwait(false);
        if (semanticModel is null) return document.Project.Solution;

        if (semanticModel.GetDeclaredSymbol(declarator, ct) is not IFieldSymbol symbol)
            return document.Project.Solution;

        var newName = ToUnderscoreCamelCase(symbol.Name);
        return await Renamer.RenameSymbolAsync(
            document.Project.Solution, symbol, new SymbolRenameOptions(), newName, ct).ConfigureAwait(false);
    }

    internal static string ToUnderscoreCamelCase(string name)
    {
        if (name.StartsWith("m_", System.StringComparison.Ordinal)) name = name.Substring(2);
        name = name.TrimStart('_');
        if (name.Length == 0) return "_field";
        return "_" + char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
