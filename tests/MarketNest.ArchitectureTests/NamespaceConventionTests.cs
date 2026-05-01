using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace MarketNest.ArchitectureTests;

/// <summary>
///     Enforces flat layer-level namespace convention from code-rules.md §2.7:
///     - Namespaces must stop at Application/Domain/Infrastructure level
///     - Sub-folder names (Commands/, Queries/, Entities/, Persistence/) must NOT appear in namespaces
/// </summary>
public class NamespaceConventionTests
{
    private static readonly Assembly[] AllModuleAssemblies =
    [
        MarketNest.Identity.AssemblyReference.Assembly,
        MarketNest.Catalog.AssemblyReference.Assembly,
        MarketNest.Cart.AssemblyReference.Assembly,
        MarketNest.Orders.AssemblyReference.Assembly,
        MarketNest.Payments.AssemblyReference.Assembly,
        MarketNest.Reviews.AssemblyReference.Assembly,
        MarketNest.Disputes.AssemblyReference.Assembly,
        MarketNest.Notifications.AssemblyReference.Assembly,
        MarketNest.Admin.AssemblyReference.Assembly,
        MarketNest.Auditing.AssemblyReference.Assembly,
        MarketNest.Promotions.AssemblyReference.Assembly
    ];

    /// <summary>
    ///     Banned sub-namespace segments that must never appear after the layer level.
    ///     e.g., MarketNest.Admin.Application.Commands is WRONG.
    /// </summary>
    private static readonly string[] BannedSubNamespaces =
    [
        ".Commands",
        ".Queries",
        ".Entities",
        ".Persistence",
        ".Repositories",
        ".Services",
        ".Validators",
        ".Seeders",
        ".Configurations",
        ".Handlers",
        ".CommandHandlers",
        ".QueryHandlers",
        ".DomainEventHandlers",
        ".IntegrationEventHandlers",
        ".Dtos"
    ];

    // ═══════════════════════════════════════════════════════════════
    // 1. No folder-level namespace segments after Application/Domain/Infrastructure
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void Namespaces_ShouldBeFlatAtLayerLevel(Assembly moduleAssembly)
    {
        var allTypes = Types.InAssembly(moduleAssembly)
            .GetTypes();

        var violations = new List<string>();

        foreach (var type in allTypes)
        {
            var ns = type.Namespace ?? "";

            // Skip types not in our module namespaces
            if (!ns.StartsWith("MarketNest.", StringComparison.Ordinal)) continue;

            foreach (var banned in BannedSubNamespaces)
            {
                if (ns.Contains(banned, StringComparison.Ordinal))
                {
                    violations.Add($"{type.FullName} → namespace '{ns}' contains banned segment '{banned}'");
                }
            }
        }

        violations.Should().BeEmpty(
            because: "Namespaces must be flat at layer level (code-rules.md §2.7). " +
                     "Sub-folders are for file organization only — they must NOT appear in the namespace. " +
                     $"Violations: {string.Join("; ", violations)}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. All types must use one of the allowed namespace patterns
    //    MarketNest.<Module> | MarketNest.<Module>.Application |
    //    MarketNest.<Module>.Domain | MarketNest.<Module>.Infrastructure
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void AllTypes_ShouldUseAllowedNamespaceLevels(Assembly moduleAssembly)
    {
        var moduleName = moduleAssembly.GetName().Name!; // e.g., "MarketNest.Admin"
        var allowedNamespaces = new HashSet<string>
        {
            moduleName,
            $"{moduleName}.Application",
            $"{moduleName}.Domain",
            $"{moduleName}.Infrastructure",
            $"{moduleName}.Migrations" // EF Core auto-generated migration classes
        };

        var allTypes = Types.InAssembly(moduleAssembly)
            .GetTypes()
            .Where(t => t.Namespace?.StartsWith("MarketNest.", StringComparison.Ordinal) == true);

        var violations = new List<string>();

        foreach (var type in allTypes)
        {
            var ns = type.Namespace ?? "";
            if (!allowedNamespaces.Contains(ns))
            {
                violations.Add($"{type.Name} → '{ns}'");
            }
        }

        violations.Should().BeEmpty(
            because: $"All types in {moduleName} must use one of the allowed namespaces: " +
                     $"{string.Join(", ", allowedNamespaces)}. " +
                     $"Violations: {string.Join("; ", violations)}");
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    public static TheoryData<Assembly> GetModuleAssemblies()
    {
        var data = new TheoryData<Assembly>();
        foreach (var asm in AllModuleAssemblies) data.Add(asm);
        return data;
    }
}
