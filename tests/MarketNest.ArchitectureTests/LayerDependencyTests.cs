using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace MarketNest.ArchitectureTests;

/// <summary>
///     Enforces layer dependency rules per architecture.md §11 and code-rules.md §3.3:
///     - Domain layer must NOT reference infrastructure (EF Core, Redis, HTTP).
///     - Application layer must NOT reference infrastructure directly.
///     - Modules must NOT reference each other — only Base.* packages.
/// </summary>
public class LayerDependencyTests
{
    // ── Module assemblies ──────────────────────────────────────────
    private static readonly Assembly IdentityAssembly = MarketNest.Identity.AssemblyReference.Assembly;
    private static readonly Assembly CatalogAssembly = MarketNest.Catalog.AssemblyReference.Assembly;
    private static readonly Assembly CartAssembly = MarketNest.Cart.AssemblyReference.Assembly;
    private static readonly Assembly OrdersAssembly = MarketNest.Orders.AssemblyReference.Assembly;
    private static readonly Assembly PaymentsAssembly = MarketNest.Payments.AssemblyReference.Assembly;
    private static readonly Assembly ReviewsAssembly = MarketNest.Reviews.AssemblyReference.Assembly;
    private static readonly Assembly DisputesAssembly = MarketNest.Disputes.AssemblyReference.Assembly;
    private static readonly Assembly NotificationsAssembly = MarketNest.Notifications.AssemblyReference.Assembly;
    private static readonly Assembly AdminAssembly = MarketNest.Admin.AssemblyReference.Assembly;
    private static readonly Assembly AuditingAssembly = MarketNest.Auditing.AssemblyReference.Assembly;
    private static readonly Assembly PromotionsAssembly = MarketNest.Promotions.AssemblyReference.Assembly;

    private static readonly Assembly[] AllModuleAssemblies =
    [
        IdentityAssembly, CatalogAssembly, CartAssembly, OrdersAssembly,
        PaymentsAssembly, ReviewsAssembly, DisputesAssembly,
        NotificationsAssembly, AdminAssembly, AuditingAssembly, PromotionsAssembly
    ];

    // ── Banned infrastructure namespaces for Domain layer ──────────
    private static readonly string[] InfrastructureNamespaces =
    [
        "Microsoft.EntityFrameworkCore",
        "StackExchange.Redis",
        "System.Net.Http",
        "Npgsql",
        "MassTransit",
        "MailKit",
        "MimeKit"
    ];

    // ── Other module namespaces (cross-module reference ban) ───────
    private static readonly string[] ModuleNamespaces =
    [
        "MarketNest.Identity",
        "MarketNest.Catalog",
        "MarketNest.Cart",
        "MarketNest.Orders",
        "MarketNest.Payments",
        "MarketNest.Reviews",
        "MarketNest.Disputes",
        "MarketNest.Notifications",
        "MarketNest.Admin",
        "MarketNest.Auditing",
        "MarketNest.Promotions"
    ];

    // ═══════════════════════════════════════════════════════════════
    // 1. Domain layer must NOT depend on infrastructure packages
    //    (code-rules.md §3.3)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void DomainLayer_ShouldNotDependOn_InfrastructurePackages(Assembly moduleAssembly)
    {
        var domainTypes = Types.InAssembly(moduleAssembly)
            .That()
            .ResideInNamespaceContaining(".Domain")
            .GetTypes();

        if (!domainTypes.Any()) return; // Module may not have domain types yet

        foreach (var infraNs in InfrastructureNamespaces)
        {
            var result = Types.InAssembly(moduleAssembly)
                .That()
                .ResideInNamespaceContaining(".Domain")
                .ShouldNot()
                .HaveDependencyOn(infraNs)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: $"Domain layer in {moduleAssembly.GetName().Name} must not reference '{infraNs}'. " +
                         $"Violations: {FormatViolations(result.FailingTypeNames)}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. Modules must NOT reference each other
    //    (architecture.md §5, §11: "Modules NEVER reference each other")
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void Module_ShouldNotReference_OtherModules(Assembly moduleAssembly)
    {
        var moduleName = moduleAssembly.GetName().Name!;
        var ownNamespace = moduleName.Replace(".csproj", ""); // e.g., "MarketNest.Identity"

        var otherModuleNamespaces = ModuleNamespaces
            .Where(ns => !ns.Equals(ownNamespace, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (var otherNs in otherModuleNamespaces)
        {
            var result = Types.InAssembly(moduleAssembly)
                .ShouldNot()
                .HaveDependencyOn(otherNs)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                because: $"Module '{moduleName}' must not reference '{otherNs}'. " +
                         $"Cross-module communication must go through Base.Common contracts or domain events. " +
                         $"Violations: {FormatViolations(result.FailingTypeNames)}");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. Application layer must NOT reference Web/Presentation
    //    (architecture.md §14)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void ApplicationLayer_ShouldNotDependOn_WebLayer(Assembly moduleAssembly)
    {
        var result = Types.InAssembly(moduleAssembly)
            .That()
            .ResideInNamespaceContaining(".Application")
            .ShouldNot()
            .HaveDependencyOn("MarketNest.Web")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Application layer in {moduleAssembly.GetName().Name} must not reference the Web host project. " +
                     $"Violations: {FormatViolations(result.FailingTypeNames)}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. Domain layer must NOT reference Application layer
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void DomainLayer_ShouldNotDependOn_ApplicationLayer(Assembly moduleAssembly)
    {
        var moduleName = moduleAssembly.GetName().Name!;
        var applicationNamespace = $"{moduleName}.Application";

        var result = Types.InAssembly(moduleAssembly)
            .That()
            .ResideInNamespaceContaining(".Domain")
            .ShouldNot()
            .HaveDependencyOn(applicationNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Domain layer in {moduleName} must not reference its own Application layer. " +
                     $"Violations: {FormatViolations(result.FailingTypeNames)}");
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

    private static string FormatViolations(IEnumerable<string>? failingTypes) =>
        failingTypes is null ? "(none)" : string.Join(", ", failingTypes);
}

