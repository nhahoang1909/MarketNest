using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace MarketNest.ArchitectureTests;

/// <summary>
///     Enforces infrastructure and persistence conventions from architecture.md §14
///     and code-rules.md §5:
///     - DbContext classes must implement IModuleDbContext
///     - Infrastructure classes must reside in Infrastructure namespace
///     - No DbContext injection in Application layer handlers
/// </summary>
public class InfrastructurePatternTests
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

    // ═══════════════════════════════════════════════════════════════
    // 1. Write DbContext classes must implement IModuleDbContext
    //    (architecture.md §14)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void WriteDbContexts_ShouldImplementIModuleDbContext(Assembly moduleAssembly)
    {
        var dbContextTypes = Types.InAssembly(moduleAssembly)
            .That()
            .HaveNameEndingWith("DbContext")
            .And()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .GetTypes()
            .Where(t => !t.Name.Contains("Read")) // ReadDbContext doesn't implement IModuleDbContext
            .ToList();

        foreach (var dbCtxType in dbContextTypes)
        {
            var implementsIModule = dbCtxType.GetInterfaces()
                .Any(i => i.Name == "IModuleDbContext");

            implementsIModule.Should().BeTrue(
                because: $"Write DbContext '{dbCtxType.FullName}' must implement IModuleDbContext " +
                         $"so DatabaseInitializer can discover it (architecture.md §14).");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 2. DbContext classes must reside in Infrastructure namespace
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void DbContexts_ShouldResideInInfrastructureNamespace(Assembly moduleAssembly)
    {
        var dbContextTypes = Types.InAssembly(moduleAssembly)
            .That()
            .HaveNameEndingWith("DbContext")
            .And()
            .AreClasses()
            .GetTypes();

        foreach (var dbCtxType in dbContextTypes)
        {
            var ns = dbCtxType.Namespace ?? "";
            ns.Should().EndWith(".Infrastructure",
                because: $"DbContext '{dbCtxType.Name}' must reside in the Infrastructure namespace, not '{ns}'.");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 3. Application layer must NOT depend on DbContext directly
    //    (ADR-025: handlers inject I{Entity}Repository or I{Entity}Query)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void ApplicationLayer_ShouldNotDependOnDbContext(Assembly moduleAssembly)
    {
        var result = Types.InAssembly(moduleAssembly)
            .That()
            .ResideInNamespaceContaining(".Application")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Application layer in {moduleAssembly.GetName().Name} must not depend on EF Core directly. " +
                     $"Use repository/query interfaces instead (ADR-025). " +
                     $"Violations: {FormatViolations(result.FailingTypeNames)}");
    }

    // ═══════════════════════════════════════════════════════════════
    // 4. Each module must have a DependencyInjection registration class
    //    (code-rules.md §2.5)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void Module_ShouldHaveDependencyInjectionRegistration(Assembly moduleAssembly)
    {
        var diTypes = Types.InAssembly(moduleAssembly)
            .That()
            .AreClasses()
            .And()
            .AreStatic()
            .And()
            .ResideInNamespaceContaining(".Infrastructure")
            .GetTypes()
            .Where(t => t.GetMethods().Any(m =>
                m.Name.Contains("Module", StringComparison.Ordinal) &&
                m.Name.StartsWith("Add", StringComparison.Ordinal) &&
                m.IsStatic))
            .ToList();

        // Allow modules still under construction to skip this check
        var hasAnyInfrastructureCode = Types.InAssembly(moduleAssembly)
            .That()
            .ResideInNamespaceContaining(".Infrastructure")
            .GetTypes()
            .Any();

        if (hasAnyInfrastructureCode)
        {
            diTypes.Should().NotBeEmpty(
                because: $"Module {moduleAssembly.GetName().Name} has infrastructure code but no " +
                         $"Add{{Module}}Module() DI registration method (code-rules.md §2.5).");
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 5. ReadDbContext must NOT implement IModuleDbContext
    //    (architecture.md §14: only write context implements it)
    // ═══════════════════════════════════════════════════════════════

    [Theory]
    [MemberData(nameof(GetModuleAssemblies))]
    public void ReadDbContexts_ShouldNotImplementIModuleDbContext(Assembly moduleAssembly)
    {
        var readDbContextTypes = Types.InAssembly(moduleAssembly)
            .That()
            .HaveNameEndingWith("ReadDbContext")
            .And()
            .AreClasses()
            .GetTypes();

        foreach (var readCtxType in readDbContextTypes)
        {
            var implementsIModule = readCtxType.GetInterfaces()
                .Any(i => i.Name == "IModuleDbContext");

            implementsIModule.Should().BeFalse(
                because: $"ReadDbContext '{readCtxType.FullName}' must NOT implement IModuleDbContext. " +
                         $"Only write-side DbContexts implement it (architecture.md §14).");
        }
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
