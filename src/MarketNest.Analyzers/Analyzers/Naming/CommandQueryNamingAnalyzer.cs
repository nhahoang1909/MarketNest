// src/MarketNest.Analyzers/Analyzers/Naming/CommandQueryNamingAnalyzer.cs
using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace MarketNest.Analyzers.Naming;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CommandQueryNamingAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor CommandRule = new(
        DiagnosticIds.MN012, "Command naming",
        "'{0}' implements ICommand but does not end with 'Command'",
        "Naming", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor QueryRule = new(
        DiagnosticIds.MN013, "Query naming",
        "'{0}' implements IQuery but does not start with 'Get' and end with 'Query'",
        "Naming", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor HandlerRule = new(
        DiagnosticIds.MN014, "Handler naming",
        "'{0}' implements ICommandHandler or IQueryHandler but does not end with 'Handler'",
        "Naming", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor EventRule = new(
        DiagnosticIds.MN015, "Event naming",
        "'{0}' implements IDomainEvent or IIntegrationEvent but does not end with 'Event'",
        "Naming", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(CommandRule, QueryRule, HandlerRule, EventRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze,
            SyntaxKind.ClassDeclaration, SyntaxKind.RecordDeclaration);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var typeDecl = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol symbol) return;

        var name = symbol.Name;
        foreach (var iface in symbol.AllInterfaces)
        {
            var ifaceName = iface.OriginalDefinition.Name;
            switch (ifaceName)
            {
                case "ICommand" when !name.EndsWith("Command", StringComparison.Ordinal):
                    context.ReportDiagnostic(Diagnostic.Create(CommandRule, typeDecl.Identifier.GetLocation(), name));
                    return;
                case "IQuery" when !name.StartsWith("Get", StringComparison.Ordinal) || !name.EndsWith("Query", StringComparison.Ordinal):
                    context.ReportDiagnostic(Diagnostic.Create(QueryRule, typeDecl.Identifier.GetLocation(), name));
                    return;
                case "ICommandHandler" when !name.EndsWith("Handler", StringComparison.Ordinal):
                    context.ReportDiagnostic(Diagnostic.Create(HandlerRule, typeDecl.Identifier.GetLocation(), name));
                    return;
                case "IQueryHandler" when !name.EndsWith("Handler", StringComparison.Ordinal):
                    context.ReportDiagnostic(Diagnostic.Create(HandlerRule, typeDecl.Identifier.GetLocation(), name));
                    return;
                case "IDomainEvent" when !name.EndsWith("Event", StringComparison.Ordinal):
                    context.ReportDiagnostic(Diagnostic.Create(EventRule, typeDecl.Identifier.GetLocation(), name));
                    return;
                case "IIntegrationEvent" when !name.EndsWith("Event", StringComparison.Ordinal):
                    context.ReportDiagnostic(Diagnostic.Create(EventRule, typeDecl.Identifier.GetLocation(), name));
                    return;
            }
        }
    }
}
