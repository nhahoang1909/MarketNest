using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Architecture;

/// <summary>
/// MN019 — QueryHandlers and CommandHandlers must never return Entity / AggregateRoot types
/// directly. They must return DTOs or result records.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class HandlerEntityReturnAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticIds.MN019,
        title: "Handler must not return entity type directly",
        messageFormat: "Handler '{0}' returns entity type '{1}' directly — return a DTO or result record instead",
        category: "Architecture",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "QueryHandlers and CommandHandlers must never expose Entity or AggregateRoot types in their return signature. Return a dedicated DTO to prevent accidental leakage of domain state and to keep the public contract stable.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.ClassDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var classDecl = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol classSymbol)
            return;

        // Find the handler interface (ICommandHandler<,> or IQueryHandler<,>)
        ITypeSymbol? resultType = null;
        foreach (var iface in classSymbol.AllInterfaces)
        {
            var name = iface.OriginalDefinition.Name;
            if ((name == "ICommandHandler" || name == "IQueryHandler") && iface.TypeArguments.Length == 2)
            {
                resultType = iface.TypeArguments[1]; // TResult is the second arg
                break;
            }
        }

        if (resultType is null) return;

        // Unwrap wrappers (Task<T>, Result<T,E>, IEnumerable<T>, IReadOnlyList<T>, List<T>, …)
        // and look for any Entity/AggregateRoot type inside the chain
        var entityType = FindEntityType(resultType);
        if (entityType is null) return;

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            classDecl.Identifier.GetLocation(),
            classSymbol.Name,
            entityType.Name));
    }

    /// <summary>Recursively unwraps generic type arguments and returns the first entity type found.</summary>
    private static INamedTypeSymbol? FindEntityType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named)
        {
            if (IsEntityOrAggregate(named)) return named;

            if (named.IsGenericType)
            {
                foreach (var arg in named.TypeArguments)
                {
                    var found = FindEntityType(arg);
                    if (found is not null) return found;
                }
            }
        }

        return null;
    }

    private static bool IsEntityOrAggregate(INamedTypeSymbol symbol)
    {
        for (var t = symbol.BaseType; t is not null; t = t.BaseType)
        {
            var name = t.OriginalDefinition.Name;
            if (name == "Entity" || name == "AggregateRoot") return true;
        }

        return false;
    }
}

